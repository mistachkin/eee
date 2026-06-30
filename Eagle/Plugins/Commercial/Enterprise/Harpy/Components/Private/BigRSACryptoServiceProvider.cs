/*
 * BigRSACryptoServiceProvider.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

//
// NOTE: Capability symbols derived from the framework target. They gate the
//       pieces of this class that do not exist on older frameworks, so the same
//       source compiles from .NET Framework 2.0 RTM (C# 2.0 / csc 2.0) through
//       modern .NET (see architecture_patterns.md, conditional compilation as
//       architecture). NET_40 is cascading (all v4.x); NET_46+ are enumerated.
//       These features are all part of the .NET Standard 2.x subset, so the
//       NET_STANDARD_2x symbols cover the portable build; the NET_CORE_2x/3x/5x
//       symbols are intentionally NOT used here -- those guard APIs present on
//       the .NET Core runtime but absent from the .NET Standard subset, which is
//       a different concern than these capabilities.
//
//         HAVE_SYSTEM_NUMERICS : System.Numerics.BigInteger exists (.NET 4.0+).
//         HAVE_RSA_PADDING_API : HashAlgorithmName / RSAEncryptionPadding /
//                                RSASignaturePadding exist (.NET 4.6+).
//         HAVE_TPL             : System.Threading.Tasks.Parallel exists (4.0+).
//
#if NET_40 || NET_STANDARD_20 || NET_STANDARD_21
#define HAVE_SYSTEM_NUMERICS
#define HAVE_TPL
#endif

#if NET_46 || NET_461 || NET_462 || NET_47 || NET_471 || NET_472 || NET_48 || NET_481 || NET_STANDARD_20 || NET_STANDARD_21
#define HAVE_RSA_PADDING_API
#endif

using System;
using System.Collections.Generic;

#if HAVE_SYSTEM_NUMERICS
using System.Numerics;
#endif

using System.Reflection;
using System.Security.Cryptography;

#if HAVE_TPL
using System.Threading.Tasks;
#endif

#if XML
using System.Xml;
#endif

using Eagle._Attributes;
using Eagle._Components.Public;
using Licensing.Components.Private;
using TraceOps = Licensing.Components.Private.CertificateTraceOps;
using RSAProvider = BigCrypto.BigRSACryptoServiceProvider;

//
// NOTE: On frameworks WITHOUT System.Numerics (.NET 2.0 / 3.5) the nested
//       BigBigInteger engine IS the BigInteger for this file, via this alias --
//       it provides the full signed BigInteger-compatible surface used below. On
//       newer frameworks the framework type is imported above and BigBigInteger
//       lies dormant. This is the single switch point for the substitution.
//
#if !HAVE_SYSTEM_NUMERICS
using BigInteger = BigCrypto.BigRSACryptoServiceProvider.BigBigInteger;
#endif

namespace BigCrypto
{
    /// <summary>
    /// Managed RSA implementation (System.Numerics.BigInteger) providing
    /// RSACryptoServiceProvider-style functionality. It implements RSAEP/RSADP
    /// (encrypt/decrypt) and RSASP1/RSAVP1 (sign/verify) per RFC 8017, with
    /// PKCS#1 v1.5 and OAEP encryption padding and PKCS#1 v1.5 and PSS signature
    /// padding, plus key generation, import/export, and CAPI blob export. Unlike
    /// the platform RSA providers it supports key sizes both below and above the
    /// platform limits, which is its reason for existing.
    /// ----------------------------------------------------------------------
    /// SECURITY HARDENING MECHANISMS
    /// ----------------------------------------------------------------------
    /// * Unified private-key primitive. Decrypt (RSADP) and Sign (RSASP1) are
    ///   the same operation m = x^d mod n and are routed through a single
    ///   method (PrivateTransform) so they cannot diverge in their defenses.
    /// * Message (base) blinding. Each private-key operation multiplies the
    ///   input by r^e for a fresh random r coprime to n, and divides the
    ///   result by r afterward (RandomBigIntegerBelow + TryModInverse). This
    ///   decorrelates the secret exponentiation from attacker-chosen inputs,
    ///   defeating chosen-ciphertext timing/remote-timing attacks and
    ///   undermining statistical side-channel collection.
    /// * Exponent blinding. In the CRT path each half-exponent is randomized
    ///   as d' = d + k*(p-1) / d + k*(q-1) with a small random k
    ///   (EXPONENT_BLIND_BITS), masking the exponent bit pattern across calls
    ///   (defense against power/EM and exponent-bit timing leakage).
    /// * Fault-injection self-check. When VerifyResultBeforeReturn is set
    ///   (default), the result is verified by recomputing result^e mod n and
    ///   comparing to the input; a mismatch aborts. This blocks the
    ///   Boneh-DeMillo-Lipton / Lenstra CRT fault attack, where a single
    ///   corrupted CRT half leaks a prime factor via gcd.
    /// * Constant-time padding removal. The PKCS#1 v1.5 and OAEP decoders
    ///   (EME_*_Decode_CT) scan the entire block with no early exit, fold every
    ///   validity condition into a single accumulator, and raise an identical
    ///   generic "Decryption error." regardless of which check failed -- the
    ///   standard Bleichenbacher / Manger countermeasure.
    /// * Constant-time signature comparison. PKCS#1 v1.5 verification RE-ENCODES
    ///   the expected block and compares it whole with FixedTimeEquals, rather
    ///   than parsing the attacker-supplied block; this avoids the BB'06-style
    ///   "loose parser" universal-forgery class and does not leak the match
    ///   position. PSS verification likewise compares H in constant time.
    /// * Input range checks. Decrypt rejects out-of-range and trivial
    ///   ciphertext representatives (0, 1, n-1) that carry no message and only
    ///   serve as oracle probes; verification rejects s >= n.
    /// * Strict key validation on import. ImportParameters requires n odd and
    ///   &gt; 1 and e odd and &gt;= 3, and for a private key with factors checks
    ///   p*q == n, the PRIMALITY of both p and q (Miller-Rabin + Baillie-PSW),
    ///   e*d == 1 (mod lcm(p-1,q-1)), and CRT-component consistency -- rejecting
    ///   malformed or maliciously crafted keys.
    /// * High-quality randomness. A single shared, thread-safe
    ///   RandomNumberGenerator feeds all key bytes, padding, salts, and blinding
    ///   factors; integer sampling uses rejection sampling to stay uniform.
    /// * Sound key generation. Primes are produced at the exact requested bit
    ///   length, screened by a small-prime sieve, required to be coprime to e,
    ///   and certified by Miller-Rabin + Baillie-PSW (or optionally the
    ///   deterministic AKS test); generated key pairs satisfy the FIPS 186-4
    ///   minimum prime-distance rule and an exact modulus bit-length check.
    /// * Best-effort secret wiping. Transient secret buffers (recovered padded
    ///   plaintext, salts, masks, PS) are zeroized via a non-elidable native
    ///   helper (see ZeroMemory) on every path, including failures.
    /// ----------------------------------------------------------------------
    /// PERFORMANCE MECHANISMS
    /// ----------------------------------------------------------------------
    /// * CRT private exponentiation. The private operation is done with the
    ///   Chinese Remainder Theorem (Garner recombination) over half-size
    ///   moduli -- roughly a 3-4x speedup over a full-width modexp.
    /// * Optional parallel CRT. At or above ParallelCrtThresholdBits the two
    ///   CRT half-exponentiations run on separate threads (Parallel.Invoke),
    ///   roughly halving latency for larger keys on multicore hosts. (This
    ///   trades throughput for latency; raise the threshold for
    ///   throughput-bound, highly-concurrent signing.)
    /// * Small exponent-blinding factor. The blinding k is only
    ///   EXPONENT_BLIND_BITS wide, which provides ample masking while avoiding
    ///   the ~2x cost of a full-width blind on every operation.
    /// * Small-prime sieve. Candidate primes are cheaply screened by trial
    ///   division against a cached sieve before any modular-exponentiation
    ///   test, discarding the large majority of composites early.
    /// * Tuned primality rounds. Because generation tests RANDOM (not
    ///   adversarial) candidates, a few Miller-Rabin rounds backed by
    ///   Baillie-PSW already drive the error far below 2^-128; import keeps the
    ///   conservative 64-round posture for adversarial inputs.
    /// * Allocation avoidance on hot paths. DigestInfo DER prefixes are
    ///   precomputed once, and the cached _keySizeBits is used instead of
    ///   recomputing BitLength(n) per signature.
    /// * The lock-free small-prime cache uses volatile publication so the
    ///   fast path needs no lock once the sieve is built.
    /// ----------------------------------------------------------------------
    /// THE BigInteger DEPENDENCY (inherent limitations, and how to improve)
    /// ----------------------------------------------------------------------
    /// System.Numerics.BigInteger is a general-purpose, variable-length numeric
    /// type, NOT a cryptographic one, and this is the single largest constraint
    /// on the security this class can offer:
    /// * It is NOT constant-time. Its add/multiply/divide/modpow run in time
    ///   and with memory-access patterns that depend on the operand VALUES
    ///   (limb counts, branch decisions, cache lines). So every secret-
    ///   dependent operation here -- the core modexp, the CRT reductions, even
    ///   the modular inverse -- leaks through timing and microarchitectural
    ///   channels in principle.
    /// * Its storage cannot be wiped. The internal uint[] is immutable and GC-
    ///   managed; secret values (d, p, q, dp, dq, qInv and intermediates)
    ///   remain in the managed heap until collected, and may be copied by the
    ///   GC or paged to disk. Dispose can only drop references, not erase bytes.
    /// * It is not Montgomery-/branch-optimized, so it is also slower than a
    ///   dedicated bignum.
    /// The blinding and fault checks above are precisely the countermeasures that
    /// make this acceptable DESPITE these properties: blinding randomizes the
    /// operands so value-dependent leakage is decorrelated from the key, and the
    /// fault check catches computation errors. They are mitigations, not cures --
    /// they reduce exploitability but cannot make a value-dependent primitive
    /// truly constant-time.
    /// If the BigInteger type were "swapped out" for a hardened bignum -- one
    /// offering constant-time, fixed-width modular arithmetic (e.g. Montgomery
    /// modexp with a fixed ladder and no secret-dependent branches/memory
    /// access), explicit zeroization of its backing store, and ideally pinned
    /// non-pageable memory -- the security of THIS class would improve directly
    /// and substantially, with no change to its algorithms:
    /// * The residual timing / cache / power / EM side channels in the core
    ///   exponentiation and CRT would be closed at the source, so security
    ///   would no longer rest on blinding alone (blinding would become
    ///   defense-in-depth rather than the primary timing defense).
    /// * Secret material could be genuinely erased after use, shrinking the
    ///   window for heap-scraping / cold-boot / swap-file disclosure that
    ///   Dispose cannot currently address.
    /// * Constant-time comparison/selection primitives could replace the
    ///   hand-rolled ones, and the operations could be faster as well.
    /// In short, the algorithmic hardening here is already in place; the dominant
    /// remaining risk is the arithmetic substrate, and upgrading that substrate
    /// is the highest-leverage way to strengthen the whole class.
    /// ----------------------------------------------------------------------
    /// OTHER INHERENT LIMITATIONS
    /// ----------------------------------------------------------------------
    /// * PKCS#1 v1.5 decryption is susceptible to Bleichenbacher padding
    ///   oracles at the PROTOCOL level: the constant-time decoder removes the
    ///   timing oracle, but the decrypt-or-throw API contract still lets a
    ///   caller that exposes success/failure act as an oracle. Prefer OAEP for
    ///   encryption and PSS for signatures; PKCS#1 v1.5 and SHA-1 are retained
    ///   only for interoperability/parity.
    /// </summary>
#if OBFUSCATION
    [Obfuscation(Feature = "renaming")]
#endif
    [ObjectId("4a04df30-625a-40da-87a6-d0f349c538c3")]
    internal sealed class BigRSACryptoServiceProvider : RSA
    {
        #region Private Constants
        /// <summary>
        /// The smallest RSA key size, in bits, this provider will generate or
        /// accept.
        /// </summary>
        private const int MINIMUM_KEY_SIZE = 512;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The largest RSA key size, in bits, this provider will generate or
        /// accept. This range deliberately exceeds the platform RSA providers
        /// (which cap at 16384 bits and reject sizes below 1024); supporting
        /// both larger and smaller keys is the reason this managed provider
        /// exists, so this ceiling must not be lowered. Very large keys are
        /// slow to generate in managed arithmetic, but the key size is
        /// operator-chosen, not attacker-chosen.
        /// </summary>
        private const int MAXIMUM_KEY_SIZE = 262144;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The key-size granularity, in bits, advertised in
        /// <see cref="LegalKeySizes" />.
        /// </summary>
        private const int SKIP_KEY_SIZE = 8;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Upper bound on the public-exponent bit length accepted by
        /// <see cref="ImportParameters" />. FIPS 186-5 mandates 65537 &lt;= e
        /// &lt; 2^256 for key generation, so every standards-compliant key is
        /// well under this; the bound limits the cost of public-key
        /// operations (m^e mod n) and blocks a denial-of-service via a
        /// maliciously huge imported exponent.
        /// </summary>
        private const int MAXIMUM_PUBLIC_EXPONENT_BITS = 256;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Number of random high bits used for exponent blinding. A small
        /// blind (relative to the prime size) suffices to defeat exponent-bit
        /// timing and power leakage while avoiding the roughly 2x cost of a
        /// full-width blind.
        /// </summary>
        private const int EXPONENT_BLIND_BITS = 128;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default RSA key size, in bits, used by the parameterless
        /// constructor.
        /// </summary>
        private const int DEFAULT_KEY_SIZE = 2048;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The default RSA public exponent (F4, 65537) used when none is
        /// specified.
        /// </summary>
        private const int DEFAULT_PUBLIC_EXPONENT = 65537;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// CryptoAPI KeyNumber value selecting a key-exchange key (matches
        /// the WinCrypt AT_KEYEXCHANGE constant); used by
        /// <see cref="ExportCspBlob" />.
        /// </summary>
        private const int AT_KEYEXCHANGE = 1;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// CryptoAPI KeyNumber value selecting a signature key (matches the
        /// WinCrypt AT_SIGNATURE constant); used by
        /// <see cref="ExportCspBlob" />.
        /// </summary>
        private const int AT_SIGNATURE = 2;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Data
        /// <summary>
        /// Shared, thread-safe cryptographic random number generator used for
        /// all key bytes, padding, salts, and blinding values.
        /// </summary>
        private static readonly RandomNumberGenerator _rng =
            RandomNumberGenerator.Create();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The legal key-size range advertised by
        /// <see cref="LegalKeySizes" />.
        /// </summary>
        private static readonly KeySizes[] _legal = new KeySizes[] {
            new KeySizes(MINIMUM_KEY_SIZE, MAXIMUM_KEY_SIZE, SKIP_KEY_SIZE)
        };

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Precomputed DER DigestInfo prefix (RFC 8017, section 9.2) for
        /// SHA-1, used by PKCS#1 v1.5 signatures; built once so the hex is
        /// not re-parsed on every operation.
        /// </summary>
        private static readonly byte[] _digestInfoSha1 =
            Hex("3021300906052B0E03021A05000414");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Precomputed DER DigestInfo prefix (RFC 8017, section 9.2) for
        /// SHA-256.
        /// </summary>
        private static readonly byte[] _digestInfoSha256 =
            Hex("3031300D060960864801650304020105000420");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Precomputed DER DigestInfo prefix (RFC 8017, section 9.2) for
        /// SHA-384.
        /// </summary>
        private static readonly byte[] _digestInfoSha384 =
            Hex("3041300D060960864801650304020205000430");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Precomputed DER DigestInfo prefix (RFC 8017, section 9.2) for
        /// SHA-512.
        /// </summary>
        private static readonly byte[] _digestInfoSha512 =
            Hex("3051300D060960864801650304020305000440");

        ///////////////////////////////////////////////////////////////////////

        // --- Prime tests: MR, BPSW, AKS, sieve ---

        // volatile: published lock-free from EnsureSmallPrimes and read
        // without the lock by the fast path and PassesSmallPrimeSieve.
        // Without volatile the reference/length publication is not guaranteed
        // on weak memory models (e.g. ARM64 / Apple Silicon).
        /// <summary>
        /// Cached array of small primes used by the small-prime sieve,
        /// published lock-free by <see cref="EnsureSmallPrimes" /> and read
        /// without the lock on the fast path.
        /// </summary>
        private static volatile int[] _smallPrimes = null;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The largest sieve limit for which <see cref="_smallPrimes" /> has
        /// been populated.
        /// </summary>
        private static volatile int _smallPrimesMax = 0;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Lock guarding the (re)computation of <see cref="_smallPrimes" />.
        /// </summary>
        private static readonly object _smallPrimesLock = new object();

        ///////////////////////////////////////////////////////////////////////

        // --- Baillie-PSW ---
        /// <summary>
        /// Small primes used for trial division in the Baillie-PSW
        /// probable-prime test.
        /// </summary>
        private static readonly int[] _bpswSmallPrimes = new int[]
        {
              2,   3,   5,   7,  11,  13,  17,  19,  23,  29,  31,  37,  41,
             43,  47,  53,  59,  61,  67,  71,  73,  79,  83,  89,  97, 101,
            103, 107, 109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167,
            173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229, 233, 239,
            241, 251, 257, 263, 269, 271, 277, 281, 283, 293, 307, 311, 313,
            317, 331, 337, 347, 349, 353, 359, 367, 373, 379, 383, 389, 397,
            401, 409, 419, 421, 431, 433, 439, 443, 449, 457, 461, 463, 467,
            479, 487, 491, 499, 503, 509, 521, 523, 541
        };
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        // --- Key material ---
        /// <summary>
        /// The RSA modulus n.
        /// </summary>
        private BigInteger _n;   // modulus

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The RSA public exponent e.
        /// </summary>
        private BigInteger _e;   // public exponent

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The RSA private exponent d.
        /// </summary>
        private BigInteger _d;   // private exponent

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The RSA CRT key components: the primes p and q, the CRT exponents
        /// dp and dq, and the coefficient qInv = q^-1 mod p.
        /// </summary>
        private BigInteger _p, _q, _dp, _dq, _qInv;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The current key size in bits (the bit length of the modulus).
        /// </summary>
        private int _keySizeBits;

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The CspParameters supplied to a constructor, retained as metadata
        /// only (this provider never binds to a platform CSP container); its
        /// KeyNumber influences the algorithm id chosen by
        /// <see cref="ExportCspBlob" />.
        /// </summary>
        private CspParameters _cspParameters = null;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        //
        // NOTE: Constructors.  All public constructors that take a key size
        //       generate a fresh key pair eagerly.  Use FromParameters() to
        //       import an existing key without generating a throwaway one.
        //
        /// <summary>
        /// Creates a provider and generates a fresh key pair using the
        /// default key size (<see cref="DEFAULT_KEY_SIZE" /> bits) and
        /// default public exponent (<see cref="DEFAULT_PUBLIC_EXPONENT" />).
        /// Key generation is performed eagerly; see
        /// <see cref="FromParameters" /> to import an existing key without
        /// generating a throwaway one.
        /// </summary>
        public BigRSACryptoServiceProvider()
            : this(DEFAULT_KEY_SIZE, DEFAULT_PUBLIC_EXPONENT, false)
        {
            // do nothing.
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a provider and generates a fresh key pair of the specified
        /// size in bits, using the default public exponent. Throws
        /// <see cref="CryptographicException" /> if the size is not legal (see
        /// <see cref="IsLegalKeySize" />).
        /// </summary>
        /// <param name="keySize">
        /// The key size in bits; must be a legal size (see
        /// <see cref="IsLegalKeySize" />).
        /// </param>
        /// <exception cref="CryptographicException">
        /// Thrown if <paramref name="keySize" /> is not a legal key size.
        /// </exception>
        public BigRSACryptoServiceProvider(
            int keySize /* in */
            )
            : this(keySize, DEFAULT_PUBLIC_EXPONENT, false)
        {
            // do nothing.
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a provider and generates a fresh key pair of the specified
        /// size in bits with the specified public exponent (which must be odd
        /// and &gt;= 3).
        /// </summary>
        /// <param name="keySize">
        /// The key size in bits.
        /// </param>
        /// <param name="publicExponent">
        /// The RSA public exponent (e.g. 65537).
        /// </param>
        /// <exception cref="CryptographicException">
        /// Thrown if the key size or public exponent is invalid.
        /// </exception>
        public BigRSACryptoServiceProvider(
            int keySize,       /* in */
            int publicExponent /* in */
            )
            : this(keySize, publicExponent, false)
        {
            // do nothing.
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Primary generating constructor. Validates the requested key size
        /// and public exponent, then generates a new RSA key pair. The public
        /// exponent must be odd and &gt;= 3; the key size must satisfy
        /// <see cref="IsLegalKeySize" />. When
        /// <paramref name="useAksForPrimeGeneration" /> is true the (slow)
        /// deterministic AKS test is used to certify the primes; otherwise
        /// the probabilistic Miller-Rabin / Baillie-PSW path is used. Throws
        /// <see cref="CryptographicException" /> on invalid arguments; the
        /// instance is left without usable key material in that case.
        /// </summary>
        /// <param name="keySize">
        /// The key size in bits.
        /// </param>
        /// <param name="publicExponent">
        /// The RSA public exponent.
        /// </param>
        /// <param name="useAksForPrimeGeneration">
        /// True to certify the generated primes with the deterministic (slow)
        /// AKS test.
        /// </param>
        /// <exception cref="CryptographicException">
        /// Thrown if the key size or public exponent is invalid.
        /// </exception>
        public BigRSACryptoServiceProvider(
            int keySize,                  /* in */
            int publicExponent,           /* in */
            bool useAksForPrimeGeneration /* in */
            )
        {
            if (!IsLegalKeySize(keySize))
                throw new CryptographicException("Unsupported key size.");

            if (publicExponent < 3 || (publicExponent & 1) == 0)
                throw new CryptographicException(
                    "Public exponent must be odd and >= 3.");

            UseAksForPrimeGeneration = useAksForPrimeGeneration;
            GenerateKeyPair(keySize, publicExponent);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a provider, generates a fresh key pair of the specified
        /// size (using the default public exponent), and retains a defensive
        /// shallow copy of the supplied <paramref name="parameters" /> as
        /// metadata only. This class is pure-managed and never imports to or
        /// exports from a real platform CSP container; the retained
        /// CspParameters are used only by <see cref="ExportCspBlob" />
        /// (specifically KeyNumber, which selects RSA_KEYX vs RSA_SIGN in the
        /// emitted blob). CryptoKeySecurity, ParentWindowHandle, and
        /// KeyPassword are intentionally ignored. Throws
        /// <see cref="ArgumentNullException" /> if
        /// <paramref name="parameters" /> is null.
        /// </summary>
        /// <param name="keySize">
        /// The key size in bits.
        /// </param>
        /// <param name="parameters">
        /// The <see cref="CspParameters" /> to retain as metadata; its
        /// KeyNumber influences <see cref="ExportCspBlob" />.
        /// </param>
        /// <exception cref="CryptographicException">
        /// Thrown if <paramref name="keySize" /> is not a legal key size.
        /// </exception>
        public BigRSACryptoServiceProvider(
            int keySize,             /* in */
            CspParameters parameters /* in */
            )
            : this(keySize, DEFAULT_PUBLIC_EXPONENT, false)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            // Shallow copy the interesting bits so later external mutations
            // do not surprise us
            CspParameters p = new CspParameters();

            p.ProviderName = parameters.ProviderName;
            p.ProviderType = parameters.ProviderType;
            p.KeyContainerName = parameters.KeyContainerName;
            p.KeyNumber = parameters.KeyNumber;
            p.Flags = parameters.Flags;

            // (CryptoKeySecurity, ParentWindowHandle, KeyPassword are ignored
            // here by design)
            _cspParameters = p;

            //
            // NOTE: We do not import/export to a real CSP container; this
            //       class is pure-managed. KeyNumber is honored by
            //       ExportCspBlob to choose RSA_KEYX vs RSA_SIGN.
            //
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a provider, generates a fresh key pair of the specified
        /// size with the specified public exponent (and optional AKS prime
        /// proving), and retains a defensive shallow copy of
        /// <paramref name="parameters" /> as metadata only (see the (int,
        /// CspParameters) overload for the semantics and limitations of the
        /// retained CspParameters). Throws
        /// <see cref="ArgumentNullException" /> if
        /// <paramref name="parameters" /> is null.
        /// </summary>
        /// <param name="keySize">
        /// The key size in bits.
        /// </param>
        /// <param name="parameters">
        /// The <see cref="CspParameters" /> to retain as metadata.
        /// </param>
        /// <param name="publicExponent">
        /// The RSA public exponent.
        /// </param>
        /// <param name="useAksForPrimeGeneration">
        /// True to certify the generated primes with the deterministic (slow)
        /// AKS test.
        /// </param>
        /// <exception cref="CryptographicException">
        /// Thrown if the key size or public exponent is invalid.
        /// </exception>
        public BigRSACryptoServiceProvider(
            int keySize,                  /* in */
            CspParameters parameters,     /* in */
            int publicExponent,           /* in */
            bool useAksForPrimeGeneration /* in */
            )
            : this(keySize, publicExponent, useAksForPrimeGeneration)
        {
            if (parameters == null)
                throw new ArgumentNullException("parameters");

            CspParameters p = new CspParameters();

            p.ProviderName = parameters.ProviderName;
            p.ProviderType = parameters.ProviderType;
            p.KeyContainerName = parameters.KeyContainerName;
            p.KeyNumber = parameters.KeyNumber;
            p.Flags = parameters.Flags;

            _cspParameters = p;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Private constructor that does NOT generate a key pair; the instance
        /// has no usable key material until <see cref="ImportParameters" /> is
        /// called. The dedicated bool parameter exists solely to disambiguate
        /// this overload from the (int keySize) generating constructor.
        /// </summary>
        /// <param name="skipKeyGeneration">
        /// True to construct the instance without generating a key pair (the
        /// caller imports one via <see cref="ImportParameters" />).
        /// </param>
        private BigRSACryptoServiceProvider(
            bool skipKeyGeneration /* in */
            )
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        // --- Policy/feature flags ---
        /// <summary>
        /// Backing field for <see cref="VerifyResultBeforeReturn" />.
        /// </summary>
        private bool _verifyResultBeforeReturn = true;
        /// <summary>
        /// Gets or sets whether each private-key result is verified, by
        /// recomputing and comparing it before returning, to defend against
        /// fault-injection (Bellcore) attacks. Defaults to true.
        /// </summary>
        public bool VerifyResultBeforeReturn
        {
            get { return _verifyResultBeforeReturn; }
            set { _verifyResultBeforeReturn = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Backing field for <see cref="UseExponentBlinding" />.
        /// </summary>
        private bool _useExponentBlinding = true;
        /// <summary>
        /// Gets or sets whether the CRT half-exponents are randomized
        /// (exponent blinding) to mask the exponent bit pattern across
        /// operations. Defaults to true.
        /// </summary>
        public bool UseExponentBlinding
        {
            get { return _useExponentBlinding; }
            set { _useExponentBlinding = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: When enabled, private-key modular exponentiation routes
        //       through the in-house BigBigInteger engine (constant-time CIOS
        //       Montgomery for small/medium moduli, Barrett+NTT for large)
        //       instead of the framework System.Numerics.BigInteger.ModPow.
        //       OFF by default; it is seeded once per instance from the
        //       UseBigBigInteger environment variable (presence => enabled)
        //       and can be overridden at runtime via this property. The fault
        //       self-check (VerifyResultBeforeReturn) still validates every
        //       result regardless of which engine is used.
        //
        /// <summary>
        /// Backing field for <see cref="UseBigBigInteger" />.
        /// </summary>
        private bool _useBigBigInteger = Configuration.DoesVariableExist(
            Constants.UseBigBigIntegerEnvVarName);
        /// <summary>
        /// Gets or sets whether private-key modular exponentiation is routed
        /// through the in-house <see cref="BigBigInteger" /> engine instead
        /// of System.Numerics; seeded once per instance from the
        /// UseBigBigInteger environment variable.
        /// </summary>
        public bool UseBigBigInteger
        {
            get { return _useBigBigInteger; }
            set { _useBigBigInteger = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Backing field for <see cref="UseParallelCrtForHugeKeys" />.
        /// </summary>
        private bool _useParallelCrtForHugeKeys = true;
        /// <summary>
        /// Gets or sets whether the two CRT half-exponentiations run on
        /// separate threads for large keys (at or above
        /// <see cref="ParallelCrtThresholdBits" />). Defaults to true.
        /// </summary>
        public bool UseParallelCrtForHugeKeys
        {
            get { return _useParallelCrtForHugeKeys; }
            set { _useParallelCrtForHugeKeys = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Backing field for <see cref="ParallelCrtThresholdBits" />.
        /// </summary>
        private int _parallelCrtThresholdBits = 2048;
        /// <summary>
        /// Gets or sets the modulus size, in bits, at or above which the two
        /// CRT half-exponentiations are parallelized (when
        /// <see cref="UseParallelCrtForHugeKeys" /> is set). Defaults to
        /// 2048.
        /// </summary>
        public int ParallelCrtThresholdBits
        {
            get { return _parallelCrtThresholdBits; }
            set { _parallelCrtThresholdBits = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Backing field for <see cref="UseSmallPrimeSieve" />.
        /// </summary>
        private bool _useSmallPrimeSieve = true;
        /// <summary>
        /// Gets or sets whether prime-candidate generation screens candidates
        /// with a small-prime sieve before the probable-prime test. Defaults
        /// to true.
        /// </summary>
        public bool UseSmallPrimeSieve
        {
            get { return _useSmallPrimeSieve; }
            set { _useSmallPrimeSieve = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Backing field for <see cref="SmallPrimeSieveLimit" />.
        /// </summary>
        private int _smallPrimeSieveLimit = 100000;
        /// <summary>
        /// Gets or sets the upper bound of the small-prime sieve used to
        /// screen prime candidates. Defaults to 100000.
        /// </summary>
        public int SmallPrimeSieveLimit
        {
            get { return _smallPrimeSieveLimit; }
            set { _smallPrimeSieveLimit = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Backing field for <see cref="UseBpswForPrimeTesting" />.
        /// </summary>
        private bool _useBpswForPrimeTesting = true;
        /// <summary>
        /// Gets or sets whether the Baillie-PSW test is used, in addition to
        /// Miller-Rabin, when testing primality during key generation.
        /// Defaults to true.
        /// </summary>
        public bool UseBpswForPrimeTesting
        {
            get { return _useBpswForPrimeTesting; }
            set { _useBpswForPrimeTesting = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Backing field for <see cref="UseAksForPrimeGeneration" />.
        /// </summary>
        private bool _useAksForPrimeGeneration = false;
        /// <summary>
        /// Gets or sets whether the deterministic (and slow) AKS primality
        /// test is used to certify generated primes. Defaults to false.
        /// </summary>
        public bool UseAksForPrimeGeneration
        {
            get { return _useAksForPrimeGeneration; }
            set { _useAksForPrimeGeneration = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        // PSS options
        /// <summary>
        /// Backing field for <see cref="PssSaltLength" />.
        /// </summary>
        private int? _pssSaltLength = null; // null => hashLen
        /// <summary>
        /// Gets or sets the PSS salt length in bytes, or null to use the hash
        /// length.
        /// </summary>
        public int? PssSaltLength
        {
            get { return _pssSaltLength; }
            set { _pssSaltLength = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Backing field for <see cref="PssVerifyEnforceSaltLength" />.
        /// </summary>
        private bool _pssVerifyEnforceSaltLength = false;
        /// <summary>
        /// Gets or sets whether PSS verification requires the recovered salt
        /// length to equal the configured <see cref="PssSaltLength" />.
        /// Defaults to false.
        /// </summary>
        public bool PssVerifyEnforceSaltLength
        {
            get { return _pssVerifyEnforceSaltLength; }
            set { _pssVerifyEnforceSaltLength = value; }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Overrides of the abstract System.Security.Cryptography.RSA
        //       base class members follow.
        //
        /// <summary>
        /// Gets the current key size in bits, or sets it. The getter returns
        /// the bit length of the current modulus. Setting the property to a
        /// value different from the current size DISCARDS the existing key
        /// and generates an entirely new key pair of the requested size using
        /// the default public exponent; setting it to the current size is a
        /// no-op.  Throws <see cref="CryptographicException" /> if the size
        /// is not legal (see <see cref="IsLegalKeySize" />).
        /// </summary>
        public override int KeySize
        {
            get { return _keySizeBits; }
            set
            {
                if (value == _keySizeBits) return;

                if (!IsLegalKeySize(value))
                    throw new CryptographicException("Unsupported key size.");

                GenerateKeyPair(value, DEFAULT_PUBLIC_EXPONENT);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets a defensive copy of the key sizes (min/max/skip, in bits) this
        /// provider supports. The returned array is cloned so callers cannot
        /// mutate the shared definition.
        /// </summary>
        public override KeySizes[] LegalKeySizes
        {
            get { return (KeySizes[])_legal.Clone(); }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the key-exchange algorithm name, for parity with the platform
        /// RSACryptoServiceProvider.
        /// </summary>
        public override string KeyExchangeAlgorithm
        {
            get { return "RSA-PKCS1-KeyEx"; }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the signature algorithm name, for parity with the platform
        /// RSACryptoServiceProvider.
        /// </summary>
        public override string SignatureAlgorithm { get { return "RSA"; } }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the read-only reference to the CspParameters that were
        /// supplied to a constructor, or null if none were. This is metadata
        /// only (this provider never binds to a real platform CSP container);
        /// its KeyNumber influences the algorithm id chosen by
        /// <see cref="ExportCspBlob" />.
        /// </summary>
        public CspParameters CspParameters
        {
            get { return _cspParameters; }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Static Methods
        /// <summary>
        /// Determines whether this managed RSA provider should be used in
        /// place of the platform RSA implementation. The decision is based
        /// solely on the presence (not the value) of the
        /// environment/configuration variable named by
        /// <c>UseBigCryptoEnvVarName</c>; if that variable
        /// exists this returns true. This method performs no cryptographic
        /// work and has no side effects other than optional trace output in
        /// DEBUG / FORCE_TRACE builds.
        /// </summary>
        /// <returns>
        /// True if the configuration variable exists (the caller should use
        /// this provider); otherwise false.
        /// </returns>
        public static bool IsEnabled()
        {
            bool result = Configuration.DoesVariableExist(
                Constants.UseBigCryptoEnvVarName);

#if DEBUG || FORCE_TRACE
            TraceOps.DebugTrace(String.Format(
                "IsEnabled: {0} via configuration.",
                result ? "Enabled" : "Disabled"),
                typeof(RSAProvider).Name, result ?
                    TracePriority.MediumHigh :
                    TracePriority.MediumLow);
#endif

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates an instance from existing RSA parameters by importing them
        /// directly, WITHOUT first generating (and immediately discarding) a
        /// throwaway key pair as the public constructors do. The supplied
        /// parameters are validated by <see cref="ImportParameters" /> (which
        /// throws <see cref="CryptographicException" /> on inconsistent or
        /// non-prime key material); this is the preferred entry point for the
        /// import-then-use pattern.
        /// </summary>
        /// <param name="parameters">
        /// The RSA parameters to import.
        /// </param>
        /// <returns>
        /// A new provider initialized with <paramref name="parameters" />.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the parameters are inconsistent or contain non-prime
        /// factors.
        /// </exception>
        public static BigRSACryptoServiceProvider FromParameters(
            RSAParameters parameters /* in */
            )
        {
            BigRSACryptoServiceProvider rsa =
                new BigRSACryptoServiceProvider(true);

            rsa.ImportParameters(parameters);

            return rsa;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Performs a power-on self-test of the managed RSA provider. It first
        /// exercises the <see cref="BigBigInteger" /> arithmetic the RSA
        /// operations rely on -- modular exponentiation, multiply, modulo, and
        /// two's-complement byte conversion -- using small known-answer
        /// vectors, then runs full RSA round-trips on a freshly generated
        /// 4096-bit key: PKCS#1 v1.5 and OAEP-SHA256 encryption, PKCS#1 v1.5
        /// and PSS signatures, rejection of a tampered signature, and an
        /// <see cref="ExportParameters" /> / <see cref="FromParameters" />
        /// re-import that re-verifies a signature. Every operation is forced
        /// through the managed engine via <see cref="UseBigBigInteger" /> so
        /// the engine is covered exactly as the RSA paths use it. The method
        /// performs no I/O and never throws: any failed check or unexpected
        /// exception is reported as a false result.
        /// </summary>
        /// <returns>
        /// True if every self-test check passed; otherwise false.
        /// </returns>
        public static bool SelfTest()
        {
            try
            {
                //
                // NOTE: BigBigInteger arithmetic, exactly as the RSA operations
                //       use it. The modulus is odd (the RSA usage), so the
                //       known-answer modexp round-trip exercises the Montgomery
                //       path; the byte round-trip checks the I2OSP/OS2IP edge.
                //
                BigBigInteger a = new BigBigInteger((long)1000003);
                BigBigInteger b = new BigBigInteger((long)1000033);

                if (a + b != new BigBigInteger((long)2000036))
                    return false;

                if (a * b != new BigBigInteger((long)1000036000099))
                    return false;

                if (b % a != new BigBigInteger((long)30))
                    return false;

                if (!(a < b) || a >= b)
                    return false;

                BigBigInteger n = new BigBigInteger((long)3233);
                BigBigInteger m = new BigBigInteger((long)65);

                BigBigInteger c = BigBigInteger.ModPow(
                    m, new BigBigInteger((long)17), n);

                if (c != new BigBigInteger((long)2790))
                    return false;

                if (BigBigInteger.ModPow(
                        c, new BigBigInteger((long)2753), n) != m)
                {
                    return false;
                }

                byte[] magnitude = b.ToByteArray();

                if (new BigBigInteger(magnitude) != b)
                    return false;

                //
                // NOTE: Full RSA round-trips on a fresh 4096-bit key, all forced
                //       through the managed BigBigInteger engine. The key size
                //       is well above what OAEP-SHA256 and PSS-SHA256 require.
                //
                BigRSACryptoServiceProvider rsa =
                    new BigRSACryptoServiceProvider(
                        4096, DEFAULT_PUBLIC_EXPONENT, false);

                try
                {
                    rsa.UseBigBigInteger = true;

                    byte[] data = new byte[16];

                    for (int i = 0; i < data.Length; i++)
                        data[i] = (byte)(i + 1);

                    byte[] hash = new byte[32];

                    for (int i = 0; i < hash.Length; i++)
                        hash[i] = (byte)(i + 7);

                    //
                    // NOTE: PKCS#1 v1.5 and OAEP-SHA256 encryption round-trips.
                    //
                    byte[] cipher = rsa.Encrypt(data, RSAEncryptionPadding.Pkcs1);
                    byte[] plain = rsa.Decrypt(cipher, RSAEncryptionPadding.Pkcs1);

                    if (plain.Length != data.Length ||
                        !FixedTimeEquals(data, 0, plain, 0, data.Length))
                    {
                        return false;
                    }

                    cipher = rsa.Encrypt(data, RSAEncryptionPadding.OaepSHA256);
                    plain = rsa.Decrypt(cipher, RSAEncryptionPadding.OaepSHA256);

                    if (plain.Length != data.Length ||
                        !FixedTimeEquals(data, 0, plain, 0, data.Length))
                    {
                        return false;
                    }

                    //
                    // NOTE: PKCS#1 v1.5 signature round-trip plus a tampered-
                    //       signature rejection.
                    //
                    byte[] signature = rsa.SignHash(
                        hash, HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pkcs1);

                    if (!rsa.VerifyHash(hash, signature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1))
                    {
                        return false;
                    }

                    signature[0] ^= 0xFF;

                    if (rsa.VerifyHash(hash, signature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pkcs1))
                    {
                        return false;
                    }

                    //
                    // NOTE: PSS signature round-trip, re-verified after an
                    //       export/re-import of the full key.
                    //
                    byte[] pssSignature = rsa.SignHash(
                        hash, HashAlgorithmName.SHA256,
                        RSASignaturePadding.Pss);

                    if (!rsa.VerifyHash(hash, pssSignature,
                            HashAlgorithmName.SHA256,
                            RSASignaturePadding.Pss))
                    {
                        return false;
                    }

                    RSAParameters parameters = rsa.ExportParameters(true);
                    BigRSACryptoServiceProvider imported =
                        FromParameters(parameters);

                    try
                    {
                        imported.UseBigBigInteger = true;

                        if (!imported.VerifyHash(hash, pssSignature,
                                HashAlgorithmName.SHA256,
                                RSASignaturePadding.Pss))
                        {
                            return false;
                        }
                    }
                    finally
                    {
                        imported.Clear();
                    }

                    return true;
                }
                finally
                {
                    rsa.Clear();
                }
            }
            catch
            {
                return false;
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Encrypts <paramref name="data" /> with the PUBLIC key using the
        /// specified padding and returns the ciphertext as a fixed-length
        /// octet string equal to the modulus byte length. Supported padding
        /// modes are PKCS#1 v1.5 (<see cref="EME_PKCS1_v1_5_Encode" />) and
        /// OAEP with SHA-1/256/384/512 (<see cref="EME_OAEP_Encode" />); any
        /// other padding throws <see cref="CryptographicException" />. The
        /// plaintext length must fit the padding scheme for the current key
        /// size, otherwise the encoder throws. This is a public-key
        /// operation, so it is not blinded and not subject to the
        /// constant-time concerns of the private path. Throws
        /// <see cref="ArgumentNullException" /> for null arguments and
        /// <see cref="CryptographicException" /> if the key is not
        /// initialized.
        /// </summary>
        /// <param name="data">
        /// The plaintext to encrypt.
        /// </param>
        /// <param name="padding">
        /// The encryption padding mode (PKCS#1 v1.5 or OAEP).
        /// </param>
        /// <returns>
        /// The ciphertext, a fixed-length octet string equal to the modulus
        /// byte length.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="data" /> or <paramref name="padding" />
        /// is null.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// Thrown if the key is not initialized or the padding mode is
        /// unsupported.
        /// </exception>
#if HAVE_RSA_PADDING_API
        public override byte[] Encrypt(
#else
        public byte[] Encrypt(
#endif
            byte[] data,                 /* in */
            RSAEncryptionPadding padding /* in */
            )
        {
            if (data == null) throw new ArgumentNullException("data");
            if (padding == null) throw new ArgumentNullException("padding");

            EnsurePublic();

            int k = ModulusByteLength();
            byte[] em;

            if (padding == RSAEncryptionPadding.Pkcs1)
            {
                em = EME_PKCS1_v1_5_Encode(data, k);
            }
            else
            {
                HashAlgorithmName oaepHash;

                if (!TryGetOaepHash(padding, out oaepHash))
                    throw new CryptographicException("Unsupported padding.");

                em = EME_OAEP_Encode(data, k, oaepHash);
            }

            BigInteger m = OS2IP(em);
            BigInteger c = BigInteger.ModPow(m, _e, _n);

            return I2OSP(c, k);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Decrypts <paramref name="data" /> with the PRIVATE key using the
        /// specified padding and returns the recovered plaintext. The
        /// ciphertext length must equal the modulus byte length; the integer
        /// it represents must be in range and is rejected if it is a trivial
        /// representative (0, 1, or n-1) that carries no message and only
        /// serves as an oracle probe. The private exponentiation is performed
        /// by <see cref="PrivateTransform" /> (message- and exponent-blinded,
        /// with an optional fault self-check). Padding is removed by the
        /// constant-time decoders (<see cref="EME_PKCS1_v1_5_Decode_CT" /> /
        /// <see cref="EME_OAEP_Decode_CT" />), which raise a generic
        /// "Decryption error." to limit padding-oracle leakage; the recovered
        /// padded buffer is always zeroized before return. Throws
        /// <see cref="ArgumentNullException" /> for null arguments,
        /// <see cref="CryptographicException" /> if the private key is
        /// unavailable, the length is wrong, the representative is out of
        /// range, or padding is invalid/unsupported.
        /// WARNING: At the protocol level, distinguishing success from failure
        ///          of PKCS#1 v1.5 decryption is an inherent Bleichenbacher
        ///          oracle. Callers must not expose decrypt success/failure to
        ///          untrusted parties; prefer OAEP.
        /// </summary>
        /// <param name="data">
        /// The ciphertext to decrypt.
        /// </param>
        /// <param name="padding">
        /// The encryption padding mode used when the data was encrypted.
        /// </param>
        /// <returns>
        /// The recovered plaintext.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="data" /> or <paramref name="padding" />
        /// is null.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// Thrown if the private key is unavailable, the ciphertext length or
        /// representative is invalid, or the padding is unsupported.
        /// </exception>
#if HAVE_RSA_PADDING_API
        public override byte[] Decrypt(
#else
        public byte[] Decrypt(
#endif
            byte[] data,                 /* in */
            RSAEncryptionPadding padding /* in */
            )
        {
            if (data == null) throw new ArgumentNullException("data");
            if (padding == null) throw new ArgumentNullException("padding");
            EnsurePrivate();

            int k = ModulusByteLength();

            if (data.Length != k)
                throw new CryptographicException(
                    "Ciphertext length must equal modulus length.");

            BigInteger c = OS2IP(data);

            // Reject out-of-range and trivial representatives (0, 1, n-1)
            // which carry no message and only serve as oracle probes.
            if (c >= _n)
                throw new CryptographicException(
                    "Ciphertext representative out of range.");

            if (c.IsZero || c.IsOne || c == _n - BigInteger.One)
                throw new CryptographicException(
                    "Ciphertext representative out of range.");

            BigInteger m = PrivateTransform(c);

            // em holds the recovered, still-padded plaintext: secret. Zero it
            // once the padding has been stripped (success or failure).
            byte[] em = I2OSP(m, k);

            try
            {
                if (padding == RSAEncryptionPadding.Pkcs1)
                {
                    return EME_PKCS1_v1_5_Decode_CT(em);
                }
                else
                {
                    HashAlgorithmName oaepHash;

                    if (!TryGetOaepHash(padding, out oaepHash))
                        throw new CryptographicException(
                            "Unsupported padding.");

                    return EME_OAEP_Decode_CT(em, oaepHash);
                }
            }
            finally
            {
                ZeroMemory(em);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Signs the pre-computed message digest <paramref name="hash" />
        /// with the PRIVATE key and returns the signature as a fixed-length
        /// octet string equal to the modulus byte length. The digest is
        /// encoded with the requested scheme -- PKCS#1 v1.5 (a DigestInfo
        /// built from <paramref name="hashAlgorithm" />, see
        /// <see cref="DigestInfo" />) or PSS (<see cref="EMSA_PSS_Encode" />,
        /// salt length from <see cref="PssSaltLength" /> or the hash length
        /// when unset) -- and the encoded message is raised to the private
        /// exponent by <see cref="PrivateTransform" /> (message- and
        /// exponent-blinded, with an optional fault self-check that aborts on
        /// a faulty result). <paramref name="hashAlgorithm" /> must be
        /// SHA-1/256/384/512 and the hash length must match it. Throws
        /// <see cref="ArgumentNullException" /> for null hash/padding, and
        /// <see cref="CryptographicException" /> if the private key is
        /// unavailable, the padding is unsupported, or the hash/key size is
        /// incompatible with the scheme.
        /// </summary>
        /// <param name="hash">
        /// The message digest to sign.
        /// </param>
        /// <param name="hashAlgorithm">
        /// The hash algorithm that produced <paramref name="hash" />
        /// (SHA-1/256/384/512).
        /// </param>
        /// <param name="padding">
        /// The signature padding mode (PKCS#1 v1.5 or PSS).
        /// </param>
        /// <returns>
        /// The signature, a fixed-length octet string equal to the modulus
        /// byte length.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="hash" /> or <paramref name="padding" />
        /// is null.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// Thrown if the private key is unavailable, the padding is
        /// unsupported, or the hash or key size is incompatible with the
        /// scheme.
        /// </exception>
#if HAVE_RSA_PADDING_API
        public override byte[] SignHash(
#else
        public byte[] SignHash(
#endif
            byte[] hash,                     /* in */
            HashAlgorithmName hashAlgorithm, /* in */
            RSASignaturePadding padding      /* in */
            )
        {
            if (hash == null) throw new ArgumentNullException("hash");
            if (padding == null) throw new ArgumentNullException("padding");

            EnsurePrivate();

            int k = ModulusByteLength();

            if (padding == RSASignaturePadding.Pkcs1)
            {
                byte[] t = DigestInfo(hashAlgorithm, hash);
                byte[] em = EMSA_PKCS1_v1_5_Encode(t, k);
                BigInteger m = OS2IP(em);
                BigInteger s = PrivateTransform(m);
                return I2OSP(s, k);
            }
            else if (padding == RSASignaturePadding.Pss)
            {
                // == BitLength(_n); avoids a per-op ToByteArray alloc
                int emBits = _keySizeBits - 1;
                int sLen = PssSaltLength.HasValue
                    ? PssSaltLength.Value : HashLen(hashAlgorithm);
                byte[] em = EMSA_PSS_Encode(hash, emBits, sLen, hashAlgorithm);
                BigInteger m = OS2IP(em);
                BigInteger s = PrivateTransform(m);
                return I2OSP(s, k);
            }
            else
            {
                throw new CryptographicException(
                    "Unsupported signature padding.");
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies that <paramref name="signature" /> is a valid signature
        /// over the digest <paramref name="hash" /> under the PUBLIC key,
        /// returning true only if it verifies. Returns false (rather than
        /// throwing) for a wrong-length signature, an out-of-range signature
        /// representative, an unsupported padding mode, or any encoding
        /// mismatch -- verification failures are not distinguished. For
        /// PKCS#1 v1.5 the expected encoded message is RE-ENCODED from the
        /// hash and compared in constant time
        /// (<see cref="FixedTimeEquals" />) rather than parsing the
        /// attacker-supplied block, which avoids malleability/parsing
        /// pitfalls. For PSS the block is checked by
        /// <see cref="EMSA_PSS_Verify" /> (or
        /// <see cref="EMSA_PSS_Verify_WithSaltLen" /> when
        /// <see cref="PssVerifyEnforceSaltLength" /> is set and a salt length
        /// is configured). Throws <see cref="ArgumentNullException" /> only
        /// for null arguments; an uninitialized key throws via
        /// <see cref="EnsurePublic" />.
        /// </summary>
        /// <param name="hash">
        /// The message digest that was signed.
        /// </param>
        /// <param name="signature">
        /// The signature to verify.
        /// </param>
        /// <param name="hashAlgorithm">
        /// The hash algorithm that produced <paramref name="hash" />.
        /// </param>
        /// <param name="padding">
        /// The signature padding mode.
        /// </param>
        /// <returns>
        /// True if <paramref name="signature" /> is valid for
        /// <paramref name="hash" />; otherwise false.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any argument is null.
        /// </exception>
#if HAVE_RSA_PADDING_API
        public override bool VerifyHash(
#else
        public bool VerifyHash(
#endif
            byte[] hash,                     /* in */
            byte[] signature,                /* in */
            HashAlgorithmName hashAlgorithm, /* in */
            RSASignaturePadding padding      /* in */
            )
        {
            if (hash == null) throw new ArgumentNullException("hash");
            if (signature == null)
                throw new ArgumentNullException("signature");
            if (padding == null) throw new ArgumentNullException("padding");
            EnsurePublic();

            int k = ModulusByteLength();
            if (signature.Length != k) return false;

            BigInteger s = OS2IP(signature);
            if (s >= _n) return false;

            BigInteger m = BigInteger.ModPow(s, _e, _n);

            if (padding == RSASignaturePadding.Pkcs1)
            {
                // Re-encode the expected EM from the hash and compare the
                // whole block in constant time. Re-encoding (rather than
                // parsing the attacker-supplied EM) avoids
                // signature-malleability/parsing ambiguities and is the
                // recommended PKCS#1 v1.5 verify pattern.
                byte[] t = DigestInfo(hashAlgorithm, hash);
                byte[] expected;

                try
                {
                    expected = EMSA_PKCS1_v1_5_Encode(t, k);
                }
                catch (CryptographicException)
                {
                    return false; // key too small for this hash
                }

                byte[] em = I2OSP(m, k);

                return FixedTimeEquals(em, 0, expected, 0, k);
            }
            else if (padding == RSASignaturePadding.Pss)
            {
                int emBits = _keySizeBits - 1;
                int emLen = (emBits + 7) / 8;
                byte[] emPrimeK = I2OSP(m, k);
                byte[] EM = emPrimeK;

                if (k != emLen)
                {
                    if (k < emLen)
                        return false;

                    EM = new byte[emLen];
                    Buffer.BlockCopy(emPrimeK, k - emLen, EM, 0, emLen);
                }

                if (PssVerifyEnforceSaltLength && PssSaltLength.HasValue)
                {
                    return EMSA_PSS_Verify_WithSaltLen(
                        hash, EM, emBits,
                        HashLen(hashAlgorithm), hashAlgorithm,
                        PssSaltLength.Value);
                }
                else
                {
                    return EMSA_PSS_Verify(
                        hash, EM, emBits,
                        HashLen(hashAlgorithm), hashAlgorithm);
                }
            }
            else
            {
                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Exports the key as an <see cref="RSAParameters" /> structure. The
        /// public components (Modulus, Exponent) are always populated. When
        /// <paramref name="includePrivateParameters" /> is true AND a private
        /// key is present, the private components are also populated: D at
        /// the full modulus byte length and the CRT components (P, Q, DP, DQ,
        /// InverseQ) each at exactly half the modulus byte length. The
        /// fixed-length padding is required for round-trip interoperability
        /// -- the platform RSACryptoServiceProvider / CAPI reject under-sized
        /// CRT arrays. Throws <see cref="CryptographicException" /> if the
        /// key is not initialized.
        /// WARNING: When private parameters are exported, the returned arrays
        ///          contain secret key material; the caller is responsible for
        ///          clearing them when done.
        /// </summary>
        /// <param name="includePrivateParameters">
        /// True to include the private key components in the result.
        /// </param>
        /// <returns>
        /// The exported RSA parameters.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the key is not initialized.
        /// </exception>
        public override RSAParameters ExportParameters(
            bool includePrivateParameters /* in */
            )
        {
            EnsurePublic();

            RSAParameters p = new RSAParameters();

            p.Modulus = ToBigEndian(_n, ModulusByteLength());
            p.Exponent = ToBigEndian(_e);

            if (includePrivateParameters && HasPrivate())
            {
                // D is modulus-length; the CRT parameters must each be exactly
                // half-modulus length. .NET's RSACryptoServiceProvider / CAPI
                // reject under-sized arrays, so pad to fixed sizes here rather
                // than emitting the minimal-length encoding.
                int cbMod = ModulusByteLength();
                int cbPrime = PrimeByteLength();

                p.D = ToBigEndian(_d, cbMod);
                p.P = ToBigEndian(_p, cbPrime);
                p.Q = ToBigEndian(_q, cbPrime);
                p.DP = ToBigEndian(_dp, cbPrime);
                p.DQ = ToBigEndian(_dq, cbPrime);
                p.InverseQ = ToBigEndian(_qInv, cbPrime);
            }
            return p;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Imports key material from an <see cref="RSAParameters" />
        /// structure, replacing any current key. Modulus and Exponent are
        /// required (the modulus must be odd and &gt; 1; the public exponent
        /// must be odd and &gt;= 3). If the private exponent D is supplied,
        /// the private key is imported; when the prime factors P and Q are
        /// also supplied the key is strictly validated: p*q must equal n,
        /// BOTH p and q must pass the probable-prime test
        /// (<see cref="IsProbablePrime" />), e*d must be congruent to 1
        /// modulo lcm(p-1, q-1), and any supplied CRT components (DP, DQ,
        /// InverseQ) must be consistent; missing CRT components are derived.
        /// Any failed check throws <see cref="CryptographicException" />.
        /// NOTE: A private key supplied as D alone (without P and Q) cannot be
        ///       validated against the modulus and is accepted as-is; it then
        ///       uses the non-CRT private path.
        /// </summary>
        /// <param name="parameters">
        /// The RSA parameters to import.
        /// </param>
        /// <exception cref="CryptographicException">
        /// Thrown if the parameters are missing required components or are
        /// inconsistent (see the summary).
        /// </exception>
        public override void ImportParameters(
            RSAParameters parameters /* in */
            )
        {
            if (parameters.Modulus == null || parameters.Exponent == null)
                throw new CryptographicException(
                    "Modulus and Exponent are required.");

            _n = FromBigEndian(parameters.Modulus);
            _e = FromBigEndian(parameters.Exponent);
            _keySizeBits = BitLength(_n);

            // Reject oversized inputs BEFORE any expensive big-integer work
            // (p*q, primality tests, e*d) to bound attacker-driven CPU cost
            // from untrusted key parameters. The modulus cap matches what
            // this class can generate (MAXIMUM_KEY_SIZE); the exponent cap
            // bounds the cost of public-key operations (m^e mod n). See those
            // constants.
            if (_keySizeBits > MAXIMUM_KEY_SIZE)
                throw new CryptographicException(
                    "Modulus exceeds the maximum supported key size.");

            if (BitLength(_e) > MAXIMUM_PUBLIC_EXPONENT_BITS)
                throw new CryptographicException(
                    "Public exponent is too large.");

            if (_n.IsEven || _n <= BigInteger.One)
                throw new CryptographicException(
                    "Modulus must be an odd integer greater than 1.");

            if (_e < 3 || _e.IsEven)
                throw new CryptographicException(
                    "Public exponent must be odd and >= 3.");

            if (parameters.D != null)
            {
                _d = FromBigEndian(parameters.D);

                _p = (parameters.P != null) ?
                    FromBigEndian(parameters.P) : BigInteger.Zero;

                _q = (parameters.Q != null) ?
                    FromBigEndian(parameters.Q) : BigInteger.Zero;

                _dp = (parameters.DP != null) ?
                    FromBigEndian(parameters.DP) : BigInteger.Zero;

                _dq = (parameters.DQ != null) ?
                    FromBigEndian(parameters.DQ) : BigInteger.Zero;

                _qInv = (parameters.InverseQ != null) ?
                    FromBigEndian(parameters.InverseQ) : BigInteger.Zero;

                if (!_p.IsZero && !_q.IsZero)
                {
                    if (_p * _q != _n)
                        throw new CryptographicException(
                            "Inconsistent key: p*q != n");

                    // Reject keys whose "primes" are not actually prime: such
                    // factors can be crafted to weaken the key or trigger
                    // incorrect CRT behavior. (Cost is paid only at import.)
                    if (!IsProbablePrime(_p) || !IsProbablePrime(_q))
                    {
                        throw new CryptographicException(
                            "Inconsistent key: p or q is not prime.");
                    }

                    BigInteger p1 = _p - BigInteger.One;
                    BigInteger q1 = _q - BigInteger.One;
                    BigInteger lambda = Lcm(p1, q1);

                    if ((_e * _d) % lambda != BigInteger.One)
                    {
                        throw new CryptographicException(
                            "Inconsistent key: e*d != 1 (mod lcm(p-1,q-1))");
                    }

                    if (!_dp.IsZero && _dp != (_d % p1))
                    {
                        throw new CryptographicException(
                            "Inconsistent key: dp != d mod (p-1)");
                    }

                    if (!_dq.IsZero && _dq != (_d % q1))
                    {
                        throw new CryptographicException(
                            "Inconsistent key: dq != d mod (q-1)");
                    }

                    if (!_qInv.IsZero && (_q * _qInv) % _p != BigInteger.One)
                    {
                        throw new CryptographicException(
                            "Inconsistent key: qInv != q^{-1} mod p");
                    }

                    if (_dp.IsZero) _dp = _d % p1;
                    if (_dq.IsZero) _dq = _d % q1;
                    if (_qInv.IsZero) _qInv = ModInverse(_q, _p);
                }

                //
                // NOTE: If D is supplied without P and Q, the private
                //       exponent cannot be validated against the modulus
                //       (that requires the factorization). Such keys are
                //       accepted as-is and fall back to the non-CRT,
                //       message-blinded private path.
                //
            }
            else
            {
                _d = _p = _q = _dp = _dq = _qInv = BigInteger.Zero;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: RSACryptoServiceProvider parity helpers. These mirror the
        //       legacy bool-based overloads where true selects OAEP (SHA-1)
        //       and false selects PKCS#1 v1.5.
        //
        /// <summary>
        /// Convenience overload mirroring
        /// RSACryptoServiceProvider.Encrypt(byte[], bool): encrypts with
        /// OAEP-SHA1 when <paramref name="fOAEP" /> is true, otherwise with
        /// PKCS#1 v1.5. Delegates to
        /// <see cref="Encrypt(byte[], RSAEncryptionPadding)" />.
        /// </summary>
        /// <param name="data">
        /// The plaintext to encrypt.
        /// </param>
        /// <param name="fOAEP">
        /// True to use OAEP-SHA1 padding; false to use PKCS#1 v1.5.
        /// </param>
        /// <returns>
        /// The ciphertext.
        /// </returns>
        public byte[] Encrypt(
            byte[] data, /* in */
            bool fOAEP   /* in */
            )
        {
            return Encrypt(data, fOAEP ?
                RSAEncryptionPadding.OaepSHA1 : RSAEncryptionPadding.Pkcs1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Convenience overload mirroring
        /// RSACryptoServiceProvider.Decrypt(byte[], bool): decrypts with
        /// OAEP-SHA1 when <paramref name="fOAEP" /> is true, otherwise with
        /// PKCS#1 v1.5. Delegates to
        /// <see cref="Decrypt(byte[], RSAEncryptionPadding)" /> (and inherits
        /// its padding-oracle warning).
        /// </summary>
        /// <param name="data">
        /// The ciphertext to decrypt.
        /// </param>
        /// <param name="fOAEP">
        /// True if the data was encrypted with OAEP-SHA1; false for PKCS#1
        /// v1.5.
        /// </param>
        /// <returns>
        /// The recovered plaintext.
        /// </returns>
        public byte[] Decrypt(
            byte[] data, /* in */
            bool fOAEP   /* in */
            )
        {
            return Decrypt(data, fOAEP ?
                RSAEncryptionPadding.OaepSHA1 : RSAEncryptionPadding.Pkcs1);
        }

        ///////////////////////////////////////////////////////////////////////

#if XML
        /// <summary>
        /// Serializes the key to the standard XML "RSAKeyValue"
        /// representation with base64-encoded, big-endian elements. The
        /// public elements (Modulus, Exponent) are always written; the
        /// private elements (P, Q, DP, DQ, InverseQ, D) are written only when
        /// <paramref name="includePrivateParameters" /> is true. Throws
        /// <see cref="CryptographicException" /> if the key is not
        /// initialized.
        /// WARNING: With private parameters included, the returned string
        ///          contains the full private key in clear text; handle and
        ///          dispose of it accordingly.
        /// </summary>
        /// <param name="includePrivateParameters">
        /// True to include the private key components in the XML.
        /// </param>
        /// <returns>
        /// The RSAKeyValue XML representation of the key.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the key is not initialized.
        /// </exception>
        public override string ToXmlString(
            bool includePrivateParameters /* in */
            )
        {
            RSAParameters p = ExportParameters(includePrivateParameters);
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement("RSAKeyValue");

            doc.AppendChild(root);

            AppendKeyElement(doc, root, "Modulus", p.Modulus);
            AppendKeyElement(doc, root, "Exponent", p.Exponent);

            if (includePrivateParameters)
            {
                AppendKeyElement(doc, root, "P", p.P);
                AppendKeyElement(doc, root, "Q", p.Q);
                AppendKeyElement(doc, root, "DP", p.DP);
                AppendKeyElement(doc, root, "DQ", p.DQ);
                AppendKeyElement(doc, root, "InverseQ", p.InverseQ);
                AppendKeyElement(doc, root, "D", p.D);
            }

            return doc.OuterXml;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Loads key material from the standard XML "RSAKeyValue"
        /// representation and imports it via <see cref="ImportParameters" />
        /// (so all the import validation applies). Modulus and Exponent are
        /// required; the private elements are read only if a "D" element is
        /// present. The XML reader is hardened against XXE: DTD processing is
        /// prohibited and external resource resolution is disabled. Throws
        /// <see cref="ArgumentNullException" /> if
        /// <paramref name="xmlString" /> is null, and
        /// <see cref="CryptographicException" /> on a missing element,
        /// invalid base64 content, or invalid/inconsistent key material.
        /// </summary>
        /// <param name="xmlString">
        /// The RSAKeyValue XML to import.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="xmlString" /> is null.
        /// </exception>
        /// <exception cref="CryptographicException">
        /// Thrown if a required element is missing or the key material is
        /// invalid.
        /// </exception>
        public override void FromXmlString(
            string xmlString /* in */
            )
        {
            if (xmlString == null)
                throw new ArgumentNullException("xmlString");
            XmlDocument doc = new XmlDocument();
            // block external resource resolution (XXE defense)
            doc.XmlResolver = null;
            XmlReaderSettings settings = new XmlReaderSettings();
            //
            // NOTE: XmlReaderSettings.DtdProcessing is .NET 4.0+; .NET 2.0/3.5
            //       use the (later deprecated) ProhibitDtd property instead.
            //
#if NET_40 || NET_STANDARD_20 || NET_STANDARD_21
            settings.DtdProcessing = DtdProcessing.Prohibit;
#else
            settings.ProhibitDtd = true;
#endif
            settings.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(
                new System.IO.StringReader(xmlString), settings))
            {
                doc.Load(reader);
            }

            bool hasD = doc.GetElementsByTagName("D").Count > 0;
            RSAParameters p = new RSAParameters();

            p.Modulus = RequireKeyElement(doc, "Modulus");
            p.Exponent = RequireKeyElement(doc, "Exponent");
            p.D = hasD ? RequireKeyElement(doc, "D") : null;
            p.P = hasD ? RequireKeyElement(doc, "P") : null;
            p.Q = hasD ? RequireKeyElement(doc, "Q") : null;
            p.DP = hasD ? RequireKeyElement(doc, "DP") : null;
            p.DQ = hasD ? RequireKeyElement(doc, "DQ") : null;
            p.InverseQ = hasD ? RequireKeyElement(doc, "InverseQ") : null;

            ImportParameters(p);
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if !HAVE_RSA_PADDING_API
        //
        // NOTE: The pre-.NET 4.6 RSA base class declares these raw primitives
        //       as abstract, so a concrete subclass must provide them. This
        //       provider exposes the higher-level
        //       Encrypt/Decrypt/SignHash/VerifyHash methods instead and does
        //       not use the raw value primitives, so they are satisfied with
        //       NotSupportedException. (On .NET 4.6+ / .NET Standard these
        //       members are absent or non-abstract, so this region is
        //       excluded.)
        //
        /// <summary>
        /// Raw RSA encryption primitive declared abstract by the pre-.NET 4.6
        /// RSA base class. This provider exposes the higher-level
        /// <see cref="Encrypt(byte[], RSAEncryptionPadding)" /> method
        /// instead and does not implement the raw primitive.
        /// </summary>
        /// <param name="rgb">
        /// The data to transform (unused).
        /// </param>
        /// <returns>
        /// This method does not return; it always throws.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Always thrown; use
        /// <see cref="Encrypt(byte[], RSAEncryptionPadding)" /> instead.
        /// </exception>
        public override byte[] EncryptValue(
            byte[] rgb /* in */
            )
        {
            throw new NotSupportedException();
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Raw RSA decryption primitive declared abstract by the pre-.NET 4.6
        /// RSA base class. This provider exposes the higher-level
        /// <see cref="Decrypt(byte[], RSAEncryptionPadding)" /> method
        /// instead and does not implement the raw primitive.
        /// </summary>
        /// <param name="rgb">
        /// The data to transform (unused).
        /// </param>
        /// <returns>
        /// This method does not return; it always throws.
        /// </returns>
        /// <exception cref="NotSupportedException">
        /// Always thrown; use
        /// <see cref="Decrypt(byte[], RSAEncryptionPadding)" /> instead.
        /// </exception>
        public override byte[] DecryptValue(
            byte[] rgb /* in */
            )
        {
            throw new NotSupportedException();
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Exports the key as a CryptoAPI (CAPI) key blob, matching
        /// RSACryptoServiceProvider.ExportCspBlob(bool): a PUBLICKEYBLOB when
        /// <paramref name="includePrivateParameters" /> is false, or a
        /// PRIVATEKEYBLOB (BLOBHEADER + RSAPUBKEY + modulus + CRT components
        /// + private exponent, all little-endian) when true. The algorithm id
        /// is CALG_RSA_SIGN when the retained CspParameters.KeyNumber is
        /// AT_SIGNATURE, otherwise CALG_RSA_KEYX (matching the platform
        /// default). The key size must be a multiple of 16 bits for a
        /// well-formed blob. Throws <see cref="CryptographicException" /> if
        /// the key is not initialized, if private parameters are requested
        /// but unavailable or incomplete, if the public exponent exceeds 32
        /// bits, or if the key size is not a multiple of 16 bits.
        /// WARNING: A private blob contains the full private key; the caller
        ///          owns clearing the returned array.
        /// </summary>
        /// <param name="includePrivateParameters">
        /// True to export a PRIVATEKEYBLOB; false for a PUBLICKEYBLOB.
        /// </param>
        /// <returns>
        /// The CryptoAPI key blob.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the key is not initialized, private parameters are
        /// requested but unavailable, the public exponent exceeds 32 bits, or
        /// the key size is not a multiple of 16 bits.
        /// </exception>
        public byte[] ExportCspBlob(
            bool includePrivateParameters /* in */
            )
        {
            EnsurePublic();

            // --- Constants from WinCrypt.h / CAPI ---
            const byte PUBLICKEYBLOB = 0x06;
            const byte PRIVATEKEYBLOB = 0x07;
            const byte CUR_BLOB_VERSION = 0x02;

            // ALG_ID choices; selected from KeyNumber below (default RSA_KEYX,
            // matching RSACryptoServiceProvider).
            const uint CALG_RSA_KEYX = 0x0000A400; // RSA key exchange
            const uint CALG_RSA_SIGN = 0x00002400; // RSA signature

            // RSAPUBKEY magic values
            const uint RSA1_MAGIC = 0x31415352; // 'RSA1' for public
            const uint RSA2_MAGIC = 0x32415352; // 'RSA2' for private

            if (includePrivateParameters && _d.IsZero)
            {
                // If caller asked for private but we don't have it, match
                // .NET behavior -> CryptographicException
                throw new CryptographicException(
                    "Private key not available for export.");
            }

            // Sizes
            int cbMod = ModulusByteLength();         // modulus length in bytes
            int bitLen = cbMod * 8;
            // CAPI stores each prime in exactly bitLen/16 bytes, so the
            // modulus bit length must be a multiple of 16 for a well-formed
            // blob. Reject unsupported sizes with a clear error instead of a
            // later overflow.
            if ((bitLen % 16) != 0)
                throw new CryptographicException(
                    "Key size must be a multiple of 16 bits for CSP blob " +
                    "export.");
            // primes are exactly half-size in CAPI blobs
            int cbPrime = cbMod / 2;

            // Select the algorithm id from the requested KeyNumber, mirroring
            // RSACryptoServiceProvider (AT_SIGNATURE -> RSA_SIGN, else
            // RSA_KEYX).
            uint aiKeyAlg =
                (_cspParameters != null &&
                 _cspParameters.KeyNumber == AT_SIGNATURE)
                ? CALG_RSA_SIGN : CALG_RSA_KEYX;

            // Public exponent must fit in 32 bits for a CSP blob
            BigInteger uintMax = new BigInteger(uint.MaxValue);
            if (_e.Sign < 0 || _e > uintMax)
                throw new CryptographicException(
                    "Public exponent is too large for a CSP blob " +
                    "(must fit in 32 bits).");
            uint pubExp = (uint)_e;

            // Build header + RSAPUBKEY
            // BLOBHEADER (PUBLICKEYSTRUC):
            //  BYTE  bType;    (PUBLICKEYBLOB=0x06 or PRIVATEKEYBLOB=0x07)
            //  BYTE  bVersion; (0x02)
            //  WORD  reserved; (0)
            //  ALG_ID aiKeyAlg; (CALG_RSA_KEYX)
            // RSAPUBKEY:
            //  DWORD magic; ('RSA1' or 'RSA2')
            //  DWORD bitlen; (modulus size in bits)
            //  DWORD pubexp; (public exponent)
            List<byte> blob = new List<byte>(
                20 + cbMod * (includePrivateParameters ? 2 : 1) +
                (includePrivateParameters ? 5 * cbPrime : 0));

            // BLOBHEADER
            // bType
            blob.Add(
                includePrivateParameters ? PRIVATEKEYBLOB : PUBLICKEYBLOB);
            // bVersion
            blob.Add(CUR_BLOB_VERSION);
            // reserved
            AppendUInt16LE(blob, 0);
            // aiKeyAlg
            AppendUInt32LE(blob, aiKeyAlg);

            // RSAPUBKEY
            AppendUInt32LE(
                blob, includePrivateParameters ? RSA2_MAGIC : RSA1_MAGIC);
            AppendUInt32LE(blob, (uint)bitLen);
            AppendUInt32LE(blob, pubExp);

            // modulus (little-endian, length = cbMod)
            byte[] modLE = ToLittleEndianUnsignedFixed(_n, cbMod);
            blob.AddRange(modLE);

            if (!includePrivateParameters)
            {
                return blob.ToArray();
            }

            // We need CRT parts; validate presence
            if (_p.IsZero || _q.IsZero || _dp.IsZero || _dq.IsZero ||
                _qInv.IsZero || _d.IsZero)
                throw new CryptographicException(
                    "Incomplete private key for CSP export.");

            // private fields (all little-endian, fixed-size): prime1 (p),
            // prime2 (q), exponent1 (dp), exponent2 (dq), coefficient (qInv),
            // privateExponent (d)
            byte[] pLE = ToLittleEndianUnsignedFixed(_p, cbPrime);
            byte[] qLE = ToLittleEndianUnsignedFixed(_q, cbPrime);
            byte[] dpLE = ToLittleEndianUnsignedFixed(_dp, cbPrime);
            byte[] dqLE = ToLittleEndianUnsignedFixed(_dq, cbPrime);
            // coefficient = q^{-1} mod p
            byte[] qiLE = ToLittleEndianUnsignedFixed(_qInv, cbPrime);
            byte[] dLE = ToLittleEndianUnsignedFixed(_d, cbMod);

            blob.AddRange(pLE);
            blob.AddRange(qLE);
            blob.AddRange(dpLE);
            blob.AddRange(dqLE);
            blob.AddRange(qiLE);
            blob.AddRange(dLE);

            return blob.ToArray();
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IDisposable Members
        /// <summary>
        /// Releases the key material held by this instance. When
        /// <paramref name="disposing" /> is true the modulus, exponents, and
        /// CRT values are reset to zero, dropping the references to their
        /// backing storage.
        /// WARNING: Because key material is stored in BigInteger values
        ///          whose internal buffers cannot be explicitly overwritten,
        ///          this only clears the references; the secret bytes persist
        ///          on the managed heap until garbage-collected (and may have
        ///          been paged). This is an inherent limitation of the
        ///          managed BigInteger design (see the class summary).
        /// </summary>
        /// <param name="disposing">
        /// True when called from <see cref="System.IDisposable.Dispose" />;
        /// false when called from the finalizer.
        /// </param>
        protected override void Dispose(
            bool disposing /* in */
            )
        {
            if (disposing)
            {
                _n = _e = _d = _p = _q = _dp = _dq = _qInv = BigInteger.Zero;
            }

            //
            // NOTE: AsymmetricAlgorithm.Dispose(bool) is abstract before .NET
            //       4.0, so there is no base implementation to chain to there.
            //
#if NET_40 || NET_STANDARD_20 || NET_STANDARD_21
            base.Dispose(disposing);
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        //
        // NOTE: Key generation.
        //
        /// <summary>
        /// Generates a complete RSA key pair of <paramref name="keyBits" />
        /// bits with the given public exponent and stores it in the instance
        /// fields. Two probable primes p and q are generated (each roughly
        /// half the key size) subject to: p != q; the FIPS 186-4 B.3.3
        /// minimum-distance rule |p - q| &gt; 2^(nlen/2 - 100); the product
        /// p*q having exactly the requested bit length; and gcd(e, lcm(p-1,
        /// q-1)) == 1. The private exponent d is the inverse of e modulo
        /// lcm(p-1, q-1), and the CRT values (dp, dq, qInv) are derived. The
        /// loop retries until all constraints are met. This routine assumes
        /// its arguments were already validated by the calling constructor.
        /// </summary>
        /// <param name="keyBits">
        /// The key size in bits.
        /// </param>
        /// <param name="publicExponent">
        /// The RSA public exponent.
        /// </param>
        private void GenerateKeyPair(
            int keyBits,       /* in */
            int publicExponent /* in */
            )
        {
            int pBits = keyBits / 2;
            int qBits = keyBits - pBits;

            BigInteger e = new BigInteger(publicExponent);

            BigInteger p, q, n, d, dp, dq, qInv;
            while (true)
            {
                p = GeneratePrime(pBits, e, UseAksForPrimeGeneration);
                do
                {
                    q = GeneratePrime(qBits, e, UseAksForPrimeGeneration);
                }
                while (p == q);

                // FIPS 186-4 B.3.3 step 5.4: |p - q| > 2^(nlen/2 - 100)
                int minDiffBits = keyBits / 2 - 100;
                if (minDiffBits > 0)
                {
                    BigInteger diff = BigInteger.Abs(p - q);
                    if (diff <= (BigInteger.One << minDiffBits)) continue;
                }

                n = p * q;
                if (BitLength(n) != keyBits) continue;

                BigInteger p1 = p - BigInteger.One;
                BigInteger q1 = q - BigInteger.One;
                BigInteger l = Lcm(p1, q1);
                if (BigInteger.GreatestCommonDivisor(e, l) != BigInteger.One)
                    continue;

                d = ModInverse(e, l);
                dp = d % p1; dq = d % q1; qInv = ModInverse(q, p);
                break;
            }

            _n = n; _e = e; _d = d;
            _p = p; _q = q; _dp = dp; _dq = dq; _qInv = qInv;
            _keySizeBits = keyBits;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Generates a random probable prime of exactly
        /// <paramref name="bits" /> bits suitable for RSA with public
        /// exponent <paramref name="e" />. Each candidate is drawn from the
        /// cryptographic RNG with the top and bottom bits forced set
        /// (guaranteeing the exact bit length and oddness), is required to
        /// satisfy gcd(candidate - 1, e) == 1 (so e is invertible modulo the
        /// prime), is screened by the small-prime sieve when enabled, and is
        /// then certified prime. Certification uses the deterministic AKS
        /// test when <paramref name="useAks" /> is true; otherwise it uses
        /// Miller-Rabin (round count scaled by size) optionally combined with
        /// Baillie-PSW. The method loops until a qualifying prime is found
        /// and returns it. Uses only the class RNG; performs no key-field
        /// mutation.
        /// </summary>
        /// <param name="bits">
        /// The exact bit length of the prime to generate.
        /// </param>
        /// <param name="e">
        /// The public exponent the prime (minus one) must be coprime to.
        /// </param>
        /// <param name="useAks">
        /// True to certify the prime with the deterministic AKS test.
        /// </param>
        /// <returns>
        /// A random probable prime of exactly <paramref name="bits" /> bits.
        /// </returns>
        private BigInteger GeneratePrime(
            int bits,     /* in */
            BigInteger e, /* in */
            bool useAks   /* in */
            )
        {
            // Miller-Rabin round counts for the pre-test. The candidates
            // tested here are RANDOMLY generated (not adversarial), so by the
            // Damgard-Landrock-Pomerance bounds only a few rounds are
            // required to drive the error probability far below 2^-128 --
            // especially with the Baillie-PSW test below (no known composite
            // counterexample) as an independent backstop. Without BPSW we
            // fall back to a conservative count. NOTE: keys arriving from an
            // untrusted source via ImportParameters are NOT validated here;
            // they use the more conservative IsProbablePrime helper (64
            // Miller-Rabin rounds plus BPSW), the correct posture for
            // adversarially-chosen inputs.
            int rounds;

            if (UseBpswForPrimeTesting)
                rounds = (bits >= 4096) ? 8 : 6;
            else
                rounds = (bits >= 4096) ? 48 : 40;

            if (UseSmallPrimeSieve)
                EnsureSmallPrimes(SmallPrimeSieveLimit);

            byte[] bytes = new byte[(bits + 7) / 8];

            //
            // NOTE: The candidate must have EXACTLY "bits" significant bits,
            //       not a whole number of bytes. The most-significant byte
            //       may be partial (1..8 used bits); we clear the unused high
            //       bits and set the true top bit at position (bits - 1), and
            //       force the low bit so the candidate is odd. Forcing the
            //       high bit of the top byte unconditionally (the previous
            //       behavior) produced primes of 8*ceil(bits/8) bits, which
            //       -- when "bits" is not a multiple of 8 -- made
            //       GenerateKeyPair's BitLength(n) == keyBits check
            //       unsatisfiable and caused an infinite loop.
            //
            // 1..8 used bits in MSB
            int topBits = bits - 8 * (bytes.Length - 1);
            // keep only the used low bits
            byte topMask = (byte)(0xFF >> (8 - topBits));
            // the actual most-significant bit
            byte topBit = (byte)(1 << (topBits - 1));

            while (true)
            {
                FillBytes(bytes);
                bytes[0] &= topMask;
                bytes[0] |= topBit;
                bytes[bytes.Length - 1] |= 0x01;

                BigInteger candidate = FromBigEndian(bytes);

                if (BigInteger.GreatestCommonDivisor(
                        candidate - BigInteger.One, e) != BigInteger.One)
                {
                    continue;
                }

                if (UseSmallPrimeSieve &&
                    !PassesSmallPrimeSieve(candidate))
                {
                    continue;
                }

                bool prime;

                if (useAks)
                {
                    prime = IsPrimeAKS(candidate);
                }
                else
                {
                    prime = IsProbablePrimeMR(candidate, rounds);

                    if (prime && UseBpswForPrimeTesting)
                        prime = IsProbablePrimeBpsw(candidate);
                }

                if (prime)
                    return candidate;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        // --- RSA primitives (blinding + CRT + optional parallel) ---

        /// <summary>
        /// The single private-key primitive m = input^d mod n, shared by both
        /// <see cref="Decrypt(byte[], RSAEncryptionPadding)" /> (RSADP) and
        /// <see cref="SignHash" /> (RSASP1): because both are the identical
        /// exponentiation, routing
        /// them through one method guarantees they receive identical
        /// side-channel and fault defenses. The input is multiplicatively
        /// blinded with a fresh random r coprime to n (input * r^e mod n),
        /// the blinded value is transformed by
        /// <see cref="RSADP_Core_WithOptions" />, and the result is unblinded
        /// with r^-1 mod n; this decorrelates the secret operation from the
        /// attacker-chosen input. When
        /// <see cref="VerifyResultBeforeReturn" /> is enabled the result is
        /// verified by recomputing result^e mod n and comparing to the
        /// original input, aborting with
        /// <see cref="CryptographicException" /> on mismatch -- a defense
        /// against fault attacks (Boneh-DeMillo-Lipton / Lenstra), where a
        /// single faulty CRT result can leak the factorization. Returns the
        /// transformed value reduced into [0, n).
        /// </summary>
        /// <param name="input">
        /// The integer to transform (a ciphertext or padded message
        /// representative).
        /// </param>
        /// <returns>
        /// <paramref name="input" /> raised to the private exponent modulo n,
        /// with blinding and the optional fault self-check applied.
        /// </returns>
        private BigInteger PrivateTransform(
            BigInteger input /* in */
            )
        {
            // Choose a random blinding factor r in [1, n-1] that is coprime
            // to n. Coprimality is established by successfully computing r^-1
            // mod n (the inverse exists iff gcd(r, n) == 1), which is also
            // needed below for unblinding. This accepts exactly the same set
            // of r values as an explicit gcd test (same uniform distribution)
            // -- it just avoids computing the gcd separately from the
            // inverse.
            BigInteger r, rInv;

            do
            {
                r = RandomBigIntegerBelow(_n);
            }
            while (r.IsZero || !TryModInverse(r, _n, out rInv));

            BigInteger rPowE = BigInteger.ModPow(r, _e, _n);
            BigInteger blindedInput = (input * rPowE) % _n;

            BigInteger blindedOutput = RSADP_Core_WithOptions(blindedInput);
            BigInteger result = (blindedOutput * rInv) % _n;
            if (result.Sign < 0) result += _n;

            if (VerifyResultBeforeReturn)
            {
                BigInteger check = BigInteger.ModPow(result, _e, _n);

                if (check != input)
                {
                    throw new CryptographicException(
                        "RSA fault detected during private-key operation.");
                }
            }

            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Core RSA private exponentiation c^d mod n. When the CRT components
        /// are present it uses the Chinese Remainder Theorem (Garner
        /// recombination) for speed; otherwise it falls back to a direct
        /// modular exponentiation with the full private exponent. When
        /// <see cref="UseExponentBlinding" /> is enabled, each CRT
        /// half-exponent is randomized as d' = d + k*(p-1) (with a small
        /// <see cref="EXPONENT_BLIND_BITS" />-bit k) to mask the exponent bit
        /// pattern. For keys at or above
        /// <see cref="ParallelCrtThresholdBits" /> the two
        /// half-exponentiations run in parallel. The caller
        /// (<see cref="PrivateTransform" />) is responsible for input/output
        /// blinding and the fault check; this method expects an
        /// already-blinded input. NOTE: this routine is NOT constant-time
        /// (BigInteger arithmetic is data-dependent); the blinding in
        /// PrivateTransform is what mitigates that.
        /// </summary>
        /// <param name="c">
        /// The ciphertext representative.
        /// </param>
        /// <returns>
        /// The message representative c^d mod n, computed via the CRT when
        /// the prime factors are available.
        /// </returns>
        private BigInteger RSADP_Core_WithOptions(
            BigInteger c /* in */
            )
        {
            if (_p.IsZero || _q.IsZero || _dp.IsZero || _dq.IsZero ||
                _qInv.IsZero)
            {
                return ModExp(c, _d, _n);
            }

            BigInteger p1 = _p - BigInteger.One;
            BigInteger q1 = _q - BigInteger.One;
            BigInteger dpB = _dp, dqB = _dq;

            if (UseExponentBlinding)
            {
                // Exponent blinding: d' = d + k*(p-1). A small k
                // (EXPONENT_BLIND_BITS) is enough to randomize the exponent's
                // bit pattern between calls; using a full-width k would
                // needlessly ~double the modexp cost.
                BigInteger kp = RandomPositiveBits(EXPONENT_BLIND_BITS);
                BigInteger kq = RandomPositiveBits(EXPONENT_BLIND_BITS);
                dpB = _dp + kp * p1;
                dqB = _dq + kq * q1;
            }

            BigInteger cp = c % _p, cq = c % _q;
            BigInteger m1 = BigInteger.Zero, m2 = BigInteger.Zero;

#if HAVE_TPL
            //
            // NOTE: With the TPL available the two CRT half-exponentiations
            //       run in parallel for large keys; without it (e.g. .NET
            //       2.0/3.5) they run serially.
            //
            if (UseParallelCrtForHugeKeys &&
                _keySizeBits >= ParallelCrtThresholdBits)
            {
                Parallel.Invoke(
                    delegate { m1 = ModExp(cp, dpB, _p); },
                    delegate { m2 = ModExp(cq, dqB, _q); }
                );
            }
            else
#endif
            {
                m1 = ModExp(cp, dpB, _p);
                m2 = ModExp(cq, dqB, _q);
            }

            BigInteger h = (((m1 - m2) * _qInv) % _p + _p) % _p;
            BigInteger m = (m2 + _q * h) % _n;
            if (m.Sign < 0) m += _n;
            return m;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Modular exponentiation dispatch for the private-key path. When
        /// <see cref="UseBigBigInteger" /> is set, this routes through the
        /// in-house BigBigInteger engine (which internally selects CIOS
        /// Montgomery or Barrett+NTT by modulus size); otherwise it uses the
        /// framework <see cref="BigInteger.ModPow" />. The implicit
        /// BigInteger/BigBigInteger conversions bridge the call. This is the
        /// single runtime switch point for the private exponentiations; the
        /// public-key operations and the fault self-check stay on
        /// BigInteger.ModPow.
        /// </summary>
        /// <param name="value">
        /// The base value.
        /// </param>
        /// <param name="exponent">
        /// The exponent.
        /// </param>
        /// <param name="modulus">
        /// The modulus.
        /// </param>
        /// <returns>
        /// <paramref name="value" /> raised to <paramref name="exponent" />
        /// modulo <paramref name="modulus" />, using the engine selected by
        /// <see cref="UseBigBigInteger" />.
        /// </returns>
        private BigInteger ModExp(
            BigInteger value,    /* in */
            BigInteger exponent, /* in */
            BigInteger modulus   /* in */
            )
        {
            if (_useBigBigInteger)
                return BigBigInteger.ModPow(value, exponent, modulus);

            return BigInteger.ModPow(value, exponent, modulus);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the modulus length in bytes, ceil(keySizeBits / 8). This
        /// is the length "k" used throughout RFC 8017 for ciphertext,
        /// signature, and encoded-message sizes.
        /// </summary>
        /// <returns>
        /// The modulus length in bytes.
        /// </returns>
        private int ModulusByteLength() { return (_keySizeBits + 7) / 8; }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the fixed byte length for each CRT parameter (P, Q, DP,
        /// DQ, InverseQ), which is half the modulus byte length (rounded up).
        /// CAPI and the platform RSACryptoServiceProvider require these
        /// components to be exactly this length on import.
        /// </summary>
        /// <returns>
        /// The fixed CRT-component length in bytes, half the
        /// <see cref="ModulusByteLength" /> rounded up.
        /// </returns>
        private int PrimeByteLength() { return (ModulusByteLength() + 1) / 2; }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns true if a private exponent is present (i.e. this instance
        /// can perform private-key operations).
        /// </summary>
        /// <returns>
        /// True if the private exponent <c>d</c> is present (so private-key
        /// operations are possible); otherwise false.
        /// </returns>
        private bool HasPrivate() { return !_d.IsZero; }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Throws <see cref="CryptographicException" /> unless the public key
        /// (modulus and exponent) is initialized. Guards public-key
        /// operations.
        /// </summary>
        private void EnsurePublic()
        {
            if (_n.IsZero || _e.IsZero)
                throw new CryptographicException("RSA key not initialized.");
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Throws <see cref="CryptographicException" /> unless both the public
        /// key and the private exponent are available. Guards private-key
        /// operations (decrypt and sign).
        /// </summary>
        private void EnsurePrivate()
        {
            EnsurePublic();
            if (_d.IsZero)
                throw new CryptographicException("Private key not available.");
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Static Methods
#if XML
        //
        // NOTE: XML helpers (extracted as named methods rather than local
        //       Func/ Action delegates, which do not exist on the .NET 2.0
        //       BCL).
        //
        /// <summary>
        /// Appends a base64-encoded, big-endian key element to an XML
        /// "RSAKeyValue" document.
        /// </summary>
        /// <param name="doc">
        /// The owning XML document.
        /// </param>
        /// <param name="root">
        /// The parent element to append to.
        /// </param>
        /// <param name="name">
        /// The element name (e.g. "Modulus").
        /// </param>
        /// <param name="value">
        /// The big-endian element bytes.
        /// </param>
        private static void AppendKeyElement(
            XmlDocument doc, /* in */
            XmlElement root, /* in */
            string name,     /* in */
            byte[] value     /* in */
            )
        {
            if (value == null) return;

            XmlElement elem = doc.CreateElement(name);

            elem.InnerText = Convert.ToBase64String(value);
            root.AppendChild(elem);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads a required base64-encoded key element from an XML
        /// "RSAKeyValue" document and returns its decoded bytes.
        /// </summary>
        /// <param name="doc">
        /// The XML document to read from.
        /// </param>
        /// <param name="name">
        /// The required element name.
        /// </param>
        /// <returns>
        /// The decoded element bytes.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the named element is missing.
        /// </exception>
        private static byte[] RequireKeyElement(
            XmlDocument doc, /* in */
            string name      /* in */
            )
        {
            XmlNodeList nodes = doc.GetElementsByTagName(name);

            if (nodes == null || nodes.Count == 0)
                throw new CryptographicException("Missing element: " + name);

            try
            {
                return Convert.FromBase64String(nodes[0].InnerText);
            }
            catch (FormatException)
            {
                throw new CryptographicException(
                    "Invalid base64 content in element: " + name);
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Message encodings and padding (RFC 8017). The "_CT" suffixed
        //       decoders are written to run in constant time with respect to
        //       the padding contents, to avoid padding-oracle side channels.
        //
        /// <summary>
        /// Applies PKCS#1 v1.5 encryption padding (EME-PKCS1-v1_5, RFC 8017
        /// section 7.2.1) to message <paramref name="m" />, producing a
        /// <paramref name="k" />-byte encoded block 0x00 || 0x02 || PS ||
        /// 0x00 || M. The padding string PS consists of (k - mLen - 3) RANDOM
        /// non-zero octets drawn from the cryptographic RNG
        /// (rejection-sampled so no byte is biased toward any value and none
        /// is zero). The scratch PS buffer is zeroized before return. Throws
        /// <see cref="CryptographicException" /> if the message is too long
        /// for the key (mLen &gt; k - 11).
        /// </summary>
        /// <param name="m">
        /// The message to encode.
        /// </param>
        /// <param name="k">
        /// The target encoded length in bytes (the modulus byte length).
        /// </param>
        /// <returns>
        /// The EME-PKCS1-v1_5 encoded message.
        /// </returns>
        private static byte[] EME_PKCS1_v1_5_Encode(
            byte[] m, /* in */
            int k     /* in */
            )
        {
            if (m.Length > k - 11)
            {
                throw new CryptographicException(
                    "Message too long for PKCS#1 v1.5.");
            }

            byte[] em = new byte[k];

            em[0] = 0x00; em[1] = 0x02;

            int psLen = k - m.Length - 3;
            byte[] ps = new byte[psLen];

            FillBytes(ps);

            // Per PKCS#1: PS must be uniformly random non-zero octets.
            // Use rejection sampling to avoid biasing any byte value.
            byte[] singleByte = new byte[1];

            for (int i = 0; i < psLen; i++)
            {
                while (ps[i] == 0x00)
                {
                    FillBytes(singleByte);
                    ps[i] = singleByte[0];
                }
            }

            Buffer.BlockCopy(ps, 0, em, 2, psLen);
            em[2 + psLen] = 0x00;
            Buffer.BlockCopy(m, 0, em, 3 + psLen, m.Length);
            ZeroMemory(ps);

            return em;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes PKCS#1 v1.5 encryption padding from the encoded block
        /// <paramref name="em" /> and returns the recovered message, in
        /// CONSTANT TIME with respect to the padding contents. The full block
        /// is always scanned (no early exit) to locate the 0x00 separator,
        /// and all validity conditions -- leading 0x00 0x02, a separator
        /// present, a padding string of at least 8 bytes, and a non-negative
        /// message length -- are accumulated into a single flag. On any
        /// failure the recovered buffer is zeroized and a GENERIC
        /// <see cref="CryptographicException" /> ("Decryption error.") is
        /// thrown, revealing nothing about which check failed. This is the
        /// Bleichenbacher-resistant decoder; see also the protocol-level
        /// warning on <see cref="Decrypt(byte[], RSAEncryptionPadding)" />.
        /// </summary>
        /// <param name="em">
        /// The encoded message to decode.
        /// </param>
        /// <returns>
        /// The recovered message, or null on a padding error (the check is
        /// constant time).
        /// </returns>
        private static byte[] EME_PKCS1_v1_5_Decode_CT(
            byte[] em /* in */
            )
        {
            int k = em.Length; int bad = 0;

            bad |= em[0] ^ 0x00; bad |= em[1] ^ 0x02;

            // Constant-time scan: always iterate every byte to find the
            // first 0x00 separator. No early exit (Bleichenbacher defense).
            int found = 0;   // becomes 1 when first 0x00 is seen
            int sepIdx = 0;  // index of first 0x00

            for (int i = 2; i < k; i++)
            {
                int isZero = ConstantTimeByteEq(em[i], 0);
                int isFirst = isZero & (1 - found);

                // Conditional select: update sepIdx only on first zero
                sepIdx = sepIdx ^ ((sepIdx ^ i) & (-isFirst));
                found |= isZero;
            }

            bad |= 1 - found;                      // no separator found

            int psLen = sepIdx - 2;

            bad |= (int)((uint)(psLen - 8) >> 31); // PS must be >= 8 bytes

            int msgStart = sepIdx + 1;
            int mLen = k - msgStart;

            bad |= (int)((uint)mLen >> 31); // mLen < 0
            mLen = mLen & ~(mLen >> 31);    // clamp to 0

            byte[] m = new byte[mLen];

            if (mLen > 0)
                Buffer.BlockCopy(em, msgStart, m, 0, mLen);

            if (bad != 0)
            {
                ZeroMemory(m);
                throw new CryptographicException("Decryption error.");
            }

            return m;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Maps an OAEP <see cref="RSAEncryptionPadding" /> to the
        /// <see cref="HashAlgorithmName" /> it uses for the OAEP hash and
        /// MGF1. Returns true and sets <paramref name="hash" /> for
        /// OAEP-SHA1/256/384/ 512; returns false (with a default hash) for
        /// any non-OAEP padding.
        /// </summary>
        /// <param name="padding">
        /// The encryption padding to inspect.
        /// </param>
        /// <param name="hash">
        /// Receives the OAEP hash algorithm name when the padding is an OAEP
        /// mode.
        /// </param>
        /// <returns>
        /// True if <paramref name="padding" /> is an OAEP mode; otherwise
        /// false.
        /// </returns>
        private static bool TryGetOaepHash(
            RSAEncryptionPadding padding, /* in */
            out HashAlgorithmName hash    /* out */
            )
        {
            if (padding == RSAEncryptionPadding.OaepSHA1)
            {
                hash = HashAlgorithmName.SHA1;
                return true;
            }

            if (padding == RSAEncryptionPadding.OaepSHA256)
            {
                hash = HashAlgorithmName.SHA256;
                return true;
            }

            if (padding == RSAEncryptionPadding.OaepSHA384)
            {
                hash = HashAlgorithmName.SHA384;
                return true;
            }

            if (padding == RSAEncryptionPadding.OaepSHA512)
            {
                hash = HashAlgorithmName.SHA512;
                return true;
            }

            hash = default(HashAlgorithmName);
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies OAEP encryption padding (EME-OAEP, RFC 8017 section 7.1.1)
        /// to message <paramref name="m" /> for a <paramref name="k" />-byte
        /// key, using <paramref name="hashAlg" /> for both the label hash and
        /// the MGF1 mask generation function, with an empty label. A random
        /// hLen-byte seed is drawn from the cryptographic RNG; the data block
        /// and seed are masked (DB then seed) per the standard. Intermediate
        /// mask/seed buffers are zeroized before return. Throws
        /// <see cref="CryptographicException" /> if the message is too long
        /// (mLen &gt; k - 2*hLen - 2).
        /// </summary>
        /// <param name="m">
        /// The message to encode.
        /// </param>
        /// <param name="k">
        /// The target encoded length in bytes.
        /// </param>
        /// <param name="hashAlg">
        /// The OAEP hash algorithm.
        /// </param>
        /// <returns>
        /// The EME-OAEP encoded message.
        /// </returns>
        private static byte[] EME_OAEP_Encode(
            byte[] m,                /* in */
            int k,                   /* in */
            HashAlgorithmName hashAlg /* in */
            )
        {
            int hLen = HashLen(hashAlg);

            if (m.Length > k - 2 * hLen - 2)
                throw new CryptographicException("Message too long for OAEP.");

            byte[] lHash = ComputeHash(hashAlg, new byte[0]);
            byte[] ps = new byte[k - m.Length - 2 * hLen - 2]; // zeros
            byte[] db = new byte[hLen + ps.Length + 1 + m.Length];

            Buffer.BlockCopy(lHash, 0, db, 0, hLen);
            db[hLen + ps.Length] = 0x01;
            Buffer.BlockCopy(m, 0, db, hLen + ps.Length + 1, m.Length);

            byte[] seed = new byte[hLen];

            FillBytes(seed);

            byte[] dbMask = MGF1(seed, db.Length, hashAlg);
            byte[] maskedDB = Xor(db, dbMask);

            byte[] seedMask = MGF1(maskedDB, hLen, hashAlg);
            byte[] maskedSeed = Xor(seed, seedMask);

            byte[] em = new byte[1 + hLen + maskedDB.Length];

            em[0] = 0x00;
            Buffer.BlockCopy(maskedSeed, 0, em, 1, hLen);
            Buffer.BlockCopy(maskedDB, 0, em, 1 + hLen, maskedDB.Length);

            ZeroMemory(dbMask);
            ZeroMemory(seedMask);
            ZeroMemory(seed);

            return em;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes OAEP padding (EME-OAEP, RFC 8017 section 7.1.2) from the
        /// encoded block <paramref name="em" /> using
        /// <paramref name="hashAlg" />, returning the recovered message, in
        /// CONSTANT TIME with respect to the padding contents. The seed and
        /// data block are unmasked, the label hash is compared, and the 0x01
        /// separator after the zero padding is located by scanning the entire
        /// block (no early exit); the leading-byte check, label-hash check,
        /// and separator check are accumulated into one flag. On any failure
        /// the recovered buffer is zeroized and a GENERIC
        /// <see cref="CryptographicException" /> ("Decryption error.") is
        /// thrown.
        /// </summary>
        /// <param name="em">
        /// The encoded message to decode.
        /// </param>
        /// <param name="hashAlg">
        /// The OAEP hash algorithm.
        /// </param>
        /// <returns>
        /// The recovered message, or null on a padding error (the check is
        /// constant time).
        /// </returns>
        private static byte[] EME_OAEP_Decode_CT(
            byte[] em,               /* in */
            HashAlgorithmName hashAlg /* in */
            )
        {
            int hLen = HashLen(hashAlg);

            if (em.Length < (2 * hLen + 2))
                throw new CryptographicException("Decryption error.");

            // Branchless leading-byte check (em[0] must be 0x00); avoids a
            // data-dependent branch on a secret byte. Any non-zero value sets
            // bad.
            int bad = 0; bad |= em[0];

            byte[] maskedSeed = new byte[hLen];

            Buffer.BlockCopy(em, 1, maskedSeed, 0, hLen);

            byte[] maskedDB = new byte[em.Length - 1 - hLen];

            Buffer.BlockCopy(em, 1 + hLen, maskedDB, 0, maskedDB.Length);

            byte[] seedMask = MGF1(maskedDB, hLen, hashAlg);
            byte[] seed = Xor(maskedSeed, seedMask);

            byte[] dbMask = MGF1(seed, maskedDB.Length, hashAlg);
            byte[] db = Xor(maskedDB, dbMask);

            byte[] lHash = ComputeHash(hashAlg, new byte[0]);

            for (int i = 0; i < hLen; i++)
                bad |= (db[i] ^ lHash[i]);

            // Constant-time scan for the 0x01 separator after zero padding.
            // Always touches every byte (no early exit).
            int found = 0;
            int sepIdx = hLen; // default if not found

            for (int i = hLen; i < db.Length; i++)
            {
                int isNonZero = 1 - ConstantTimeByteEq(db[i], 0);
                int isFirst = isNonZero & (1 - found);
                // Conditional select: record index of first non-zero byte

                sepIdx = sepIdx ^ ((sepIdx ^ i) & (-isFirst));
                // If first non-zero byte is not 0x01, set bad
                bad |= isFirst & (1 - ConstantTimeByteEq(db[i], 1));
                found |= isNonZero;
            }

            bad |= 1 - found; // no separator found

            int msgIdx = sepIdx + 1;
            int mLen = db.Length - msgIdx;

            bad |= (int)((uint)mLen >> 31); // mLen < 0
            mLen = mLen & ~(mLen >> 31);     // clamp to 0

            byte[] m = new byte[mLen];

            if (mLen > 0)
                Buffer.BlockCopy(db, msgIdx, m, 0, mLen);

            ZeroMemory(seedMask);
            ZeroMemory(dbMask);
            ZeroMemory(seed);

            if (bad != 0)
            {
                ZeroMemory(m);
                throw new CryptographicException("Decryption error.");
            }

            return m;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Applies PKCS#1 v1.5 signature padding (EMSA-PKCS1-v1_5, RFC 8017
        /// section 9.2) to the already-built DigestInfo
        /// <paramref name="T" />, producing a <paramref name="k" />-byte
        /// encoded block 0x00 || 0x01 || PS || 0x00 || T, where PS is (k -
        /// tLen - 3) bytes of 0xFF. Unlike the encryption padding, PS here is
        /// deterministic (all 0xFF), as the standard requires. Throws
        /// <see cref="CryptographicException" /> if the DigestInfo does not
        /// fit the key (tLen &gt; k - 11). This routine is used both to
        /// produce signatures and (by re-encoding) to verify them.
        /// </summary>
        /// <param name="T">
        /// The DER DigestInfo to encode.
        /// </param>
        /// <param name="k">
        /// The target encoded length in bytes.
        /// </param>
        /// <returns>
        /// The EMSA-PKCS1-v1_5 encoded message.
        /// </returns>
        private static byte[] EMSA_PKCS1_v1_5_Encode(
            byte[] T, /* in */
            int k     /* in */
            )
        {
            if (T.Length > k - 11)
            {
                throw new CryptographicException(
                    "Intended encoded message length too short.");
            }

            byte[] EM = new byte[k];

            EM[0] = 0x00; EM[1] = 0x01;

            int psLen = k - T.Length - 3;

            for (int i = 0; i < psLen; i++)
                EM[2 + i] = 0xFF;

            EM[2 + psLen] = 0x00;
            Buffer.BlockCopy(T, 0, EM, 3 + psLen, T.Length);

            return EM;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: RSA-PSS encoding and verification (RFC 8017 section 9.1).
        //
        /// <summary>
        /// Produces an EMSA-PSS encoded message (RFC 8017 section 9.1.1) over
        /// the message hash <paramref name="mHash" /> for an encoded-message
        /// strength of <paramref name="emBits" /> bits, using a random salt
        /// of <paramref name="sLen" /> bytes (from the cryptographic RNG) and
        /// <paramref name="hashAlg" /> for the hash and MGF1. The result is
        /// the maskedDB || H || 0xBC block with the leading unused bits
        /// cleared. Salt and intermediate buffers are zeroized before return.
        /// Throws <see cref="CryptographicException" /> if the parameters do
        /// not fit (emLen &lt; hLen + sLen + 2).
        /// </summary>
        /// <param name="mHash">
        /// The message digest.
        /// </param>
        /// <param name="emBits">
        /// The encoded-message length in bits (one less than the key bit
        /// length).
        /// </param>
        /// <param name="sLen">
        /// The salt length in bytes.
        /// </param>
        /// <param name="hashAlg">
        /// The hash algorithm.
        /// </param>
        /// <returns>
        /// The EMSA-PSS encoded message.
        /// </returns>
        private static byte[] EMSA_PSS_Encode(
            byte[] mHash,             /* in */
            int emBits,               /* in */
            int sLen,                 /* in */
            HashAlgorithmName hashAlg /* in */
            )
        {
            int hLen = HashLen(hashAlg);
            int emLen = (emBits + 7) / 8;

            if (emLen < hLen + sLen + 2)
                throw new CryptographicException("Encoding error.");

            byte[] salt = new byte[sLen];

            FillBytes(salt);

            byte[] Mprime = new byte[8 + hLen + sLen];

            // first 8 zeros
            Buffer.BlockCopy(mHash, 0, Mprime, 8, hLen);
            Buffer.BlockCopy(salt, 0, Mprime, 8 + hLen, sLen);

            byte[] H = ComputeHash(hashAlg, Mprime);

            int psLen = emLen - sLen - hLen - 2;
            byte[] DB = new byte[psLen + 1 + sLen];

            DB[psLen] = 0x01;
            Buffer.BlockCopy(salt, 0, DB, psLen + 1, sLen);

            byte[] dbMask = MGF1(H, emLen - hLen - 1, hashAlg);
            byte[] maskedDB = Xor(DB, dbMask);

            int unusedBits = 8 * emLen - emBits;

            if (unusedBits > 0)
            {
                byte mask = (byte)(0xFF >> unusedBits);
                maskedDB[0] &= mask;
            }

            byte[] EM = new byte[emLen];

            Buffer.BlockCopy(maskedDB, 0, EM, 0, maskedDB.Length);
            Buffer.BlockCopy(H, 0, EM, maskedDB.Length, H.Length);
            EM[EM.Length - 1] = 0xBC;

            ZeroMemory(salt);
            ZeroMemory(Mprime);
            ZeroMemory(dbMask);

            return EM;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies an EMSA-PSS encoded message (RFC 8017 section 9.1.2):
        /// confirms the 0xBC trailer and cleared leading bits, unmasks DB,
        /// validates the zero padding and 0x01 separator, recovers the salt
        /// (whose length is inferred from the block), recomputes H over the
        /// message hash <paramref name="mHash" /> and salt, and compares it
        /// to the embedded H in constant time
        /// (<see cref="FixedTimeEquals" />). The salt length is NOT
        /// constrained here (any valid length is accepted); use
        /// <see cref="EMSA_PSS_Verify_WithSaltLen" /> to require a specific
        /// salt length. Returns true only if every check passes; scratch
        /// buffers are zeroized before return. Never throws for a malformed
        /// block -- it returns false.
        /// </summary>
        /// <param name="mHash">
        /// The message digest.
        /// </param>
        /// <param name="EM">
        /// The encoded message recovered from the signature.
        /// </param>
        /// <param name="emBits">
        /// The encoded-message length in bits.
        /// </param>
        /// <param name="hLen">
        /// The hash length in bytes.
        /// </param>
        /// <param name="hashAlg">
        /// The hash algorithm.
        /// </param>
        /// <returns>
        /// True if the PSS encoding is valid; otherwise false.
        /// </returns>
        private static bool EMSA_PSS_Verify(
            byte[] mHash,             /* in */
            byte[] EM,                /* in */
            int emBits,               /* in */
            int hLen,                 /* in */
            HashAlgorithmName hashAlg /* in */
            )
        {
            int emLen = EM.Length;
            if (emLen < hLen + 2) return false;
            if (EM[emLen - 1] != 0xBC) return false;

            int unusedBits = 8 * emLen - emBits;
            if (unusedBits > 0)
            {
                byte mask = (byte)(0xFF >> unusedBits);
                if ((EM[0] & ~mask) != 0) return false;
            }

            int dbLen = emLen - hLen - 1;
            byte[] maskedDB = new byte[dbLen];
            Buffer.BlockCopy(EM, 0, maskedDB, 0, dbLen);
            byte[] H = new byte[hLen];
            Buffer.BlockCopy(EM, dbLen, H, 0, hLen);

            byte[] dbMask = MGF1(H, dbLen, hashAlg);
            byte[] DB = Xor(maskedDB, dbMask);
            if (unusedBits > 0)
            {
                byte mask2 = (byte)(0xFF >> unusedBits);
                DB[0] &= mask2;
            }

            int idx = 0; while (idx < DB.Length && DB[idx] == 0x00) idx++;
            if (idx >= DB.Length || DB[idx] != 0x01)
            {
                ZeroMemory(dbMask);
                ZeroMemory(DB);
                return false;
            }
            idx++;
            int sLen = DB.Length - idx;

            if (sLen < 0)
            {
                ZeroMemory(dbMask);
                ZeroMemory(DB);
                return false;
            }

            byte[] salt = new byte[sLen];
            if (sLen > 0) Buffer.BlockCopy(DB, idx, salt, 0, sLen);

            byte[] Mprime = new byte[8 + hLen + sLen];
            Buffer.BlockCopy(mHash, 0, Mprime, 8, hLen);
            if (sLen > 0) Buffer.BlockCopy(salt, 0, Mprime, 8 + hLen, sLen);

            byte[] H2 = ComputeHash(hashAlg, Mprime);
            bool ok = FixedTimeEquals(H, 0, H2, 0, hLen);

            ZeroMemory(dbMask);
            ZeroMemory(DB);
            ZeroMemory(Mprime);
            ZeroMemory(salt);
            return ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Identical to <see cref="EMSA_PSS_Verify" /> except that the
        /// recovered salt length must exactly equal
        /// <paramref name="requiredSaltLen" />; any other salt length causes
        /// verification to fail (return false). Used when
        /// <see cref="PssVerifyEnforceSaltLength" /> is set and an explicit
        /// <see cref="PssSaltLength" /> is configured, to reject signatures
        /// that do not use the mandated salt length. Returns true only if
        /// every check passes; scratch buffers are zeroized before return.
        /// </summary>
        /// <param name="mHash">
        /// The message digest.
        /// </param>
        /// <param name="EM">
        /// The encoded message recovered from the signature.
        /// </param>
        /// <param name="emBits">
        /// The encoded-message length in bits.
        /// </param>
        /// <param name="hLen">
        /// The hash length in bytes.
        /// </param>
        /// <param name="hashAlg">
        /// The hash algorithm.
        /// </param>
        /// <param name="requiredSaltLen">
        /// The exact salt length, in bytes, that the encoding must use.
        /// </param>
        /// <returns>
        /// True if the PSS encoding is valid and uses exactly
        /// <paramref name="requiredSaltLen" /> salt bytes; otherwise false.
        /// </returns>
        private static bool EMSA_PSS_Verify_WithSaltLen(
            byte[] mHash,              /* in */
            byte[] EM,                 /* in */
            int emBits,                /* in */
            int hLen,                  /* in */
            HashAlgorithmName hashAlg, /* in */
            int requiredSaltLen        /* in */
            )
        {
            int emLen = EM.Length;
            if (emLen < hLen + 2) return false;
            if (EM[emLen - 1] != 0xBC) return false;

            int unusedBits = 8 * emLen - emBits;
            if (unusedBits > 0)
            {
                byte mask = (byte)(0xFF >> unusedBits);
                if ((EM[0] & ~mask) != 0) return false;
            }

            int dbLen = emLen - hLen - 1;
            byte[] maskedDB = new byte[dbLen];
            Buffer.BlockCopy(EM, 0, maskedDB, 0, dbLen);
            byte[] H = new byte[hLen];
            Buffer.BlockCopy(EM, dbLen, H, 0, hLen);

            byte[] dbMask = MGF1(H, dbLen, hashAlg);
            byte[] DB = Xor(maskedDB, dbMask);
            if (unusedBits > 0)
            {
                byte mask2 = (byte)(0xFF >> unusedBits);
                DB[0] &= mask2;
            }

            int idx = 0; while (idx < DB.Length && DB[idx] == 0x00) idx++;
            if (idx >= DB.Length || DB[idx] != 0x01)
            {
                ZeroMemory(dbMask);
                ZeroMemory(DB);
                return false;
            }
            idx++;
            int sLen = DB.Length - idx;

            if (sLen < 0)
            {
                ZeroMemory(dbMask);
                ZeroMemory(DB);
                return false;
            }
            if (sLen != requiredSaltLen)
            {
                ZeroMemory(dbMask);
                ZeroMemory(DB);
                return false;
            }

            byte[] salt = new byte[sLen];
            if (sLen > 0) Buffer.BlockCopy(DB, idx, salt, 0, sLen);

            byte[] Mprime = new byte[8 + hLen + sLen];
            Buffer.BlockCopy(mHash, 0, Mprime, 8, hLen);
            if (sLen > 0) Buffer.BlockCopy(salt, 0, Mprime, 8 + hLen, sLen);

            byte[] H2 = ComputeHash(hashAlg, Mprime);
            bool ok = FixedTimeEquals(H, 0, H2, 0, hLen);

            ZeroMemory(dbMask);
            ZeroMemory(DB);
            ZeroMemory(Mprime);
            ZeroMemory(salt);
            return ok;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the ASN.1 DigestInfo structure for PKCS#1 v1.5 signatures
        /// (RFC 8017 section 9.2): the fixed DER algorithm-identifier prefix
        /// for <paramref name="alg" /> followed by the raw
        /// <paramref name="hash" />. The hash length is validated against the
        /// algorithm (SHA-1 = 20, SHA-256 = 32, SHA-384 = 48, SHA-512 = 64
        /// bytes); a mismatch or unsupported algorithm throws
        /// <see cref="CryptographicException" />. Returns the concatenated
        /// prefix || hash.
        /// </summary>
        /// <param name="alg">
        /// The hash algorithm that produced <paramref name="hash" />.
        /// </param>
        /// <param name="hash">
        /// The hash value.
        /// </param>
        /// <returns>
        /// The DER DigestInfo (the algorithm prefix followed by the hash).
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the hash length does not match <paramref name="alg" />
        /// or the algorithm is unsupported.
        /// </exception>
        private static byte[] DigestInfo(
            HashAlgorithmName alg, /* in */
            byte[] hash            /* in */
            )
        {
            byte[] prefix;
            int len = hash.Length;
            if (alg.Name == "SHA1")
            {
                if (len != 20)
                    throw new CryptographicException(
                        "Bad hash length for SHA1.");

                prefix = _digestInfoSha1;
            }
            else if (alg.Name == "SHA256")
            {
                if (len != 32)
                    throw new CryptographicException(
                        "Bad hash length for SHA256.");

                prefix = _digestInfoSha256;
            }
            else if (alg.Name == "SHA384")
            {
                if (len != 48)
                    throw new CryptographicException(
                        "Bad hash length for SHA384.");

                prefix = _digestInfoSha384;
            }
            else if (alg.Name == "SHA512")
            {
                if (len != 64)
                    throw new CryptographicException(
                        "Bad hash length for SHA512.");

                prefix = _digestInfoSha512;
            }
            else
            {
                throw new CryptographicException(
                    "Unsupported hash algorithm: " + alg.Name);
            }

            byte[] di = new byte[prefix.Length + hash.Length];
            Buffer.BlockCopy(prefix, 0, di, 0, prefix.Length);
            Buffer.BlockCopy(hash, 0, di, prefix.Length, hash.Length);
            return di;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the output length in bytes of the given hash algorithm
        /// (SHA-1 = 20, SHA-256 = 32, SHA-384 = 48, SHA-512 = 64). Throws
        /// <see cref="CryptographicException" /> for an unsupported algorithm.
        /// </summary>
        /// <param name="alg">
        /// The hash algorithm.
        /// </param>
        /// <returns>
        /// The output length, in bytes, of <paramref name="alg" />.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the algorithm is unsupported.
        /// </exception>
        private static int HashLen(
            HashAlgorithmName alg /* in */
            )
        {
            if (alg.Name == "SHA1") return 20;
            if (alg.Name == "SHA256") return 32;
            if (alg.Name == "SHA384") return 48;
            if (alg.Name == "SHA512") return 64;
            throw new CryptographicException(
                "Unsupported hash algorithm: " + alg.Name);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the digest of <paramref name="data" /> using the named
        /// hash algorithm (SHA-1/256/384/512), creating and disposing a fresh
        /// hash instance per call. Throws
        /// <see cref="CryptographicException" /> for an unsupported
        /// algorithm. Used for the OAEP label hash, PSS hashing, and other
        /// one-shot digests.
        /// </summary>
        /// <param name="alg">
        /// The hash algorithm.
        /// </param>
        /// <param name="data">
        /// The data to hash.
        /// </param>
        /// <returns>
        /// The hash of <paramref name="data" />.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the algorithm is unsupported.
        /// </exception>
        private static byte[] ComputeHash(
            HashAlgorithmName alg, /* in */
            byte[] data            /* in */
            )
        {
            if (alg.Name == "SHA1")
            {
                using (SHA1 h = SHA1.Create()) { return h.ComputeHash(data); }
            }
            if (alg.Name == "SHA256")
            {
                using (SHA256 h = SHA256.Create())
                {
                    return h.ComputeHash(data);
                }
            }
            if (alg.Name == "SHA384")
            {
                using (SHA384 h = SHA384.Create())
                {
                    return h.ComputeHash(data);
                }
            }
            if (alg.Name == "SHA512")
            {
                using (SHA512 h = SHA512.Create())
                {
                    return h.ComputeHash(data);
                }
            }
            throw new CryptographicException(
                "Unsupported hash algorithm: " + alg.Name);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// MGF1 mask generation function (RFC 8017 appendix B.2.1) based on
        /// the given hash. Generates a <paramref name="maskLen" />-byte mask
        /// by hashing <paramref name="seed" /> concatenated with a 32-bit
        /// big-endian counter (0, 1, 2, ...) and concatenating the outputs,
        /// truncated to the requested length. Used to mask DB and seed in
        /// OAEP and DB in PSS.
        /// </summary>
        /// <param name="seed">
        /// The seed.
        /// </param>
        /// <param name="maskLen">
        /// The desired mask length in bytes.
        /// </param>
        /// <param name="alg">
        /// The hash algorithm.
        /// </param>
        /// <returns>
        /// The MGF1 mask of <paramref name="maskLen" /> bytes.
        /// </returns>
        private static byte[] MGF1(
            byte[] seed,          /* in */
            int maskLen,          /* in */
            HashAlgorithmName alg /* in */
            )
        {
            int hLen = HashLen(alg);
            byte[] t = new byte[maskLen];
            byte[] block = new byte[seed.Length + 4];
            Buffer.BlockCopy(seed, 0, block, 0, seed.Length);

            using (HashAlgorithm hasher = CreateHashAlgorithm(alg))
            {
                int done = 0; uint i = 0;
                while (done < maskLen)
                {
                    block[seed.Length] = (byte)((i >> 24) & 0xFF);
                    block[seed.Length + 1] = (byte)((i >> 16) & 0xFF);
                    block[seed.Length + 2] = (byte)((i >> 8) & 0xFF);
                    block[seed.Length + 3] = (byte)(i & 0xFF);

                    byte[] digest = hasher.ComputeHash(block);
                    int toCopy = hLen < (maskLen - done)
                        ? hLen : (maskLen - done);
                    Buffer.BlockCopy(digest, 0, t, done, toCopy);
                    done += toCopy; i++;
                }
            }
            return t;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates a new, reusable <see cref="HashAlgorithm" /> instance for
        /// the named algorithm (SHA-1/256/384/512). The caller owns disposing
        /// it; used by <see cref="MGF1" /> where a single hasher is reused
        /// across counter blocks. Throws
        /// <see cref="CryptographicException" /> for an unsupported
        /// algorithm.
        /// </summary>
        /// <param name="alg">
        /// The hash algorithm to create.
        /// </param>
        /// <returns>
        /// A new <see cref="System.Security.Cryptography.HashAlgorithm" />
        /// instance.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if the algorithm is unsupported.
        /// </exception>
        private static HashAlgorithm CreateHashAlgorithm(
            HashAlgorithmName alg /* in */
            )
        {
            if (alg.Name == "SHA1") return SHA1.Create();
            if (alg.Name == "SHA256") return SHA256.Create();
            if (alg.Name == "SHA384") return SHA384.Create();
            if (alg.Name == "SHA512") return SHA512.Create();
            throw new CryptographicException(
                "Unsupported hash algorithm: " + alg.Name);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Lazily builds (once, then caches) the table of all primes up to
        /// <paramref name="max" /> using a Sieve of Eratosthenes, for use by
        /// <see cref="PassesSmallPrimeSieve" />. Thread-safe via
        /// double-checked locking; the cache only grows. The cached fields
        /// are volatile so the table reference and its bound are published
        /// safely to lock-free readers on weak memory models. A no-op if a
        /// sufficiently large table already exists.
        /// </summary>
        /// <param name="max">
        /// The upper bound up to which small primes must be available.
        /// </param>
        private static void EnsureSmallPrimes(
            int max /* in */
            )
        {
            if (_smallPrimes != null && _smallPrimesMax >= max) return;

            lock (_smallPrimesLock)
            {
                // double-check
                if (_smallPrimes != null && _smallPrimesMax >= max) return;

                bool[] isComposite = new bool[max + 1];
                int count = 0;
                for (int i = 2; i * i <= max; i++)
                {
                    if (!isComposite[i])
                    {
                        for (int j = i * i; j <= max; j += i)
                            isComposite[j] = true;
                    }
                }
                for (int i = 2; i <= max; i++) if (!isComposite[i]) count++;
                int[] primes = new int[count];
                int idx = 0;
                for (int i = 2; i <= max; i++)
                    if (!isComposite[i]) primes[idx++] = i;

                _smallPrimes = primes; _smallPrimesMax = max;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Fast composite pre-screen for prime generation: returns false if
        /// <paramref name="n" /> is divisible by any cached small prime (and
        /// is not that prime itself), otherwise true ("possibly prime, not
        /// ruled out"). This cheaply discards the great majority of random
        /// composite candidates before the expensive Miller-Rabin / BPSW
        /// tests. Returns true if the table has not been built.
        /// </summary>
        /// <param name="n">
        /// The candidate to screen.
        /// </param>
        /// <returns>
        /// True if <paramref name="n" /> is not divisible by any sieved small
        /// prime; otherwise false.
        /// </returns>
        private static bool PassesSmallPrimeSieve(
            BigInteger n /* in */
            )
        {
            if (_smallPrimes == null) return true;
            for (int i = 0; i < _smallPrimes.Length; i++)
            {
                int p = _smallPrimes[i];
                if (n == p) return true;
                if (n % p == 0) return false;
            }
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Probable-prime test used to validate imported RSA factors. It
        /// combines 64 rounds of random-base Miller-Rabin (an adversarial
        /// false-positive probability below 2^-128, the correct standard for
        /// untrusted inputs) with the Baillie-PSW test (for which no
        /// composite counterexample is known) as an independent cross-check.
        /// Both have zero false negatives, so legitimate keys always pass.
        /// </summary>
        /// <param name="n">
        /// The candidate to test.
        /// </param>
        /// <returns>
        /// True if <paramref name="n" /> is probably prime; otherwise false.
        /// </returns>
        private static bool IsProbablePrime(BigInteger n)
        {
            if (n < 2) return false;
            if (!IsProbablePrimeMR(n, 64)) return false;
            return IsProbablePrimeBpsw(n);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Miller-Rabin probabilistic primality test with
        /// <paramref name="rounds" /> independent random bases drawn from the
        /// class RNG. Returns true if <paramref name="n" /> is a probable
        /// prime (never a false negative: a true prime passes every round)
        /// and false if it is definitely composite. For an ADVERSARIAL
        /// composite the per-round error is at most 1/4, so the
        /// false-positive probability is at most 4^-rounds. Even small
        /// composites are handled correctly.
        /// </summary>
        /// <param name="n">
        /// The candidate to test.
        /// </param>
        /// <param name="rounds">
        /// The number of random-base Miller-Rabin rounds.
        /// </param>
        /// <returns>
        /// True if <paramref name="n" /> passes all rounds; otherwise false.
        /// </returns>
        private static bool IsProbablePrimeMR(
            BigInteger n, /* in */
            int rounds    /* in */
            )
        {
            if (n < 2) return false;
            if (n == 2 || n == 3) return true;
            if ((n & 1) == 0) return false;

            BigInteger d = n - 1; int s = 0;
            while ((d & 1) == 0) { d >>= 1; s++; }

            byte[] aBuf = new byte[n.ToByteArray().Length];
            for (int i = 0; i < rounds; i++)
            {
                BigInteger a;
                do
                {
                    FillBytes(aBuf);
                    a = FromLittleEndianUnsigned(aBuf);
                    if (a.Sign < 0) a = -a;
                    a %= (n - 3); a += 2; // 2..n-2
                } while (a <= 1 || a >= n - 1);

                BigInteger x = BigInteger.ModPow(a, d, n);
                if (x == 1 || x == n - 1) continue;

                bool cont = false;
                for (int r = 1; r < s; r++)
                {
                    x = BigInteger.ModPow(x, 2, n);
                    if (x == n - 1) { cont = true; break; }
                }
                if (!cont) return false;
            }
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Baillie-PSW probable-prime test: small-prime trial division, a
        /// perfect-square reject, a base-2 strong Miller-Rabin test, and a
        /// strong Lucas-Selfridge test. No composite is known to pass it, and
        /// it has zero false negatives on real primes.
        /// </summary>
        /// <param name="n">
        /// The candidate to test.
        /// </param>
        /// <returns>
        /// True if <paramref name="n" /> passes the Baillie-PSW test;
        /// otherwise false.
        /// </returns>
        private static bool IsProbablePrimeBpsw(BigInteger n)
        {
            if (n < 2) return false;
            for (int i = 0; i < _bpswSmallPrimes.Length; i++)
            {
                int p = _bpswSmallPrimes[i];
                if (n == p) return true;
                if (n % p == 0) return false;
            }
            if ((n & 1) == 0) return false;
            if (IsPerfectSquare_BI(n)) return false;
            if (!MillerRabinBase2(n)) return false;
            return StrongLucasSelfridgePrp(n);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The strong (Miller-Rabin) probable-prime test to the single fixed
        /// base 2, the first stage of Baillie-PSW. Returns true if
        /// <paramref name="n" /> is a strong probable prime base 2 (a true
        /// prime always passes), otherwise false. By itself this admits the
        /// base-2 strong pseudoprimes; the subsequent Lucas test is what
        /// makes the combination strong.
        /// </summary>
        /// <param name="n">
        /// The candidate to test.
        /// </param>
        /// <returns>
        /// True if <paramref name="n" /> is a strong probable prime to base
        /// 2; otherwise false.
        /// </returns>
        private static bool MillerRabinBase2(
            BigInteger n /* in */
            )
        {
            BigInteger d = n - 1; int s = 0;
            while ((d & 1) == 0) { d >>= 1; s++; }
            BigInteger x = BigInteger.ModPow(new BigInteger(2), d, n);
            if (x == 1 || x == n - 1) return true;
            for (int r = 1; r < s; r++)
            {
                x = BigInteger.ModPow(x, 2, n);
                if (x == n - 1) return true;
            }
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The strong Lucas-Selfridge probable-prime test, the second stage
        /// of Baillie-PSW. Selects the discriminant D by Selfridge's method A
        /// (the first of 5, -7, 9, -11, ... with Jacobi(D, n) = -1; a Jacobi
        /// value of 0 proves n composite), sets P = 1 and Q = (1 - D) / 4,
        /// and evaluates the Lucas sequence at d where n + 1 = d * 2^s (d
        /// odd). Returns true if U_d == 0 (mod n) OR V_{d*2^r} == 0 (mod n)
        /// for some 0 &lt;= r &lt; s (the two strong-test conditions),
        /// otherwise false. Returns false early if gcd(n, Q) is a nontrivial
        /// factor. A true prime always passes.
        /// </summary>
        /// <param name="n">
        /// The candidate to test.
        /// </param>
        /// <returns>
        /// True if <paramref name="n" /> is a strong Lucas-Selfridge probable
        /// prime; otherwise false.
        /// </returns>
        private static bool StrongLucasSelfridgePrp(
            BigInteger n /* in */
            )
        {
            long D = 5;
            while (true)
            {
                int j = JacobiSymbol(new BigInteger(D), n);
                if (j == 0) return false;
                if (j == -1) break;
                if (D > 0) D = -(D + 2); else D = -D + 2;
            }

            BigInteger bigD = new BigInteger(D);
            BigInteger P = BigInteger.One;
            BigInteger Q = (BigInteger.One - bigD) >> 2;

            BigInteger g = BigInteger.GreatestCommonDivisor(
                n, Q.Sign < 0 ? -Q : Q);
            if (g > BigInteger.One && g < n) return false;

            BigInteger d = n + 1; int s = 0;
            while ((d & 1) == 0) { d >>= 1; s++; }

            BigInteger U, V, Qk;
            LucasSequenceMod(n, P, Q, bigD, d, out U, out V, out Qk);

            // Strong Lucas test, with n + 1 = d * 2^s (d odd): n is a strong
            // Lucas probable prime if U_d == 0 (mod n) OR V_{d*2^r} == 0 (mod
            // n) for some 0 <= r < s. The U_d == 0 condition is essential:
            // for a prime, U_{n+1} = U_d * product(V_{d*2^r}) == 0, and the
            // majority of primes satisfy the U_d factor rather than one of
            // the V factors. (Omitting this check is what previously rejected
            // most real primes.)
            if (U % n == 0) return true;   // U_d == 0 (mod n)
            if (V % n == 0) return true;   // V_{d*2^0} == 0 (mod n)

            for (int r = 1; r < s; r++)
            {
                V = (V * V) % n;
                BigInteger twoQk = (Qk << 1) % n;
                V = V - twoQk; V %= n; if (V.Sign < 0) V += n;
                Qk = (Qk * Qk) % n;
                if (V.IsZero) return true;
            }
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the Lucas sequence terms U_k and V_k (and Q^k) modulo
        /// <paramref name="n" /> for parameters P, Q with discriminant
        /// <paramref name="D" /> = P^2 - 4Q, using the left-to-right binary
        /// "double-and-add" ladder (the half/double identities, with division
        /// by 2 done via the modular inverse of 2). On return
        /// <paramref name="U" /> = U_k mod n, <paramref name="V" /> = V_k mod
        /// n, and <paramref name="Qk" /> = Q^k mod n, each reduced into [0,
        /// n). The caller (<see cref="StrongLucasSelfridgePrp" />) uses these
        /// for the strong Lucas conditions.
        /// </summary>
        /// <param name="n">
        /// The modulus.
        /// </param>
        /// <param name="P">
        /// The Lucas parameter P.
        /// </param>
        /// <param name="Q">
        /// The Lucas parameter Q.
        /// </param>
        /// <param name="D">
        /// The Lucas discriminant D.
        /// </param>
        /// <param name="k">
        /// The index k.
        /// </param>
        /// <param name="U">
        /// Receives U_k mod <paramref name="n" />.
        /// </param>
        /// <param name="V">
        /// Receives V_k mod <paramref name="n" />.
        /// </param>
        /// <param name="Qk">
        /// Receives Q^k mod <paramref name="n" />.
        /// </param>
        private static void LucasSequenceMod(
            BigInteger n,   /* in */
            BigInteger P,   /* in */
            BigInteger Q,   /* in */
            BigInteger D,   /* in */
            BigInteger k,   /* in */
            out BigInteger U,  /* out */
            out BigInteger V,  /* out */
            out BigInteger Qk  /* out */
            )
        {
            U = BigInteger.Zero;
            V = (BigInteger)2 % n;
            Qk = BigInteger.One % n;

            BigInteger inv2 = ((n + 1) >> 1) % n;
            BigInteger Uk = U, Vk = V, Qk_local = Qk;

            int bitLen = BitLength(k);
            if (bitLen == 0) { U = Uk; V = Vk; Qk = Qk_local; return; }

            for (int i = bitLen - 1; i >= 0; i--)
            {
                BigInteger U2m = (Uk * Vk) % n;
                BigInteger V2m = (Vk * Vk) % n;
                BigInteger twoQm = (Qk_local << 1) % n;
                V2m = V2m - twoQm;
                V2m %= n; if (V2m.Sign < 0) V2m += n;
                BigInteger Q2m = (Qk_local * Qk_local) % n;

                if (((k >> i) & BigInteger.One) != BigInteger.Zero)
                {
                    BigInteger tU = (P * U2m + V2m) % n;

                    if (tU.Sign < 0) tU += n;
                    BigInteger tV = (D * U2m + P * V2m) % n;

                    if (tV.Sign < 0) tV += n;

                    Uk = (tU * inv2) % n;
                    Vk = (tV * inv2) % n;
                    Qk_local = (Q2m * Q) % n;
                }
                else
                {
                    Uk = U2m; Vk = V2m; Qk_local = Q2m;
                }
            }
            U = Uk; V = Vk; Qk = Qk_local;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the Jacobi symbol (a / n) for odd positive
        /// <paramref name="n" />, returning -1, 0, or +1 using the quadratic
        /// reciprocity / factor-out-twos algorithm. A result of 0 means
        /// gcd(a, n) &gt; 1 (so n is composite when used in the Lucas test).
        /// Used to select the Lucas-Selfridge discriminant. Throws
        /// <see cref="ArgumentException" /> if <paramref name="n" /> is not
        /// odd and positive.
        /// </summary>
        /// <param name="a">
        /// The numerator.
        /// </param>
        /// <param name="n">
        /// The odd, positive modulus.
        /// </param>
        /// <returns>
        /// The Jacobi symbol (a/n): -1, 0, or +1.
        /// </returns>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="n" /> is not odd and positive.
        /// </exception>
        private static int JacobiSymbol(
            BigInteger a, /* in */
            BigInteger n  /* in */
            )
        {
            if (n.Sign <= 0 || ((n & 1) == 0))
                throw new ArgumentException("Jacobi requires odd positive n.");
            if (a.IsZero) return 0;

            int j = 1;
            a %= n;
            if (a.Sign < 0)
            {
                a = -a;
                if (((int)(n % 4)) == 3) j = -j;
            }

            while (!a.IsZero)
            {
                int s = 0;
                while ((a & 1) == 0) { a >>= 1; s++; }
                if ((s & 1) != 0)
                {
                    int nMod8 = (int)(n % 8);
                    if (nMod8 == 3 || nMod8 == 5) j = -j;
                }

                if (((int)(a % 4)) == 3 && ((int)(n % 4)) == 3) j = -j;
                BigInteger t = a; a = n % t; n = t;
            }
            return n == BigInteger.One ? j : 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns true if <paramref name="n" /> is a perfect square (computed
        /// via the integer square root). Negative values return false; 0 and 1
        /// return true. Baillie-PSW excludes perfect squares before the Lucas
        /// stage (a perfect square can never have Jacobi(D, n) = -1).
        /// </summary>
        /// <param name="n">
        /// The value to test.
        /// </param>
        /// <returns>
        /// True if <paramref name="n" /> is a perfect square; otherwise
        /// false.
        /// </returns>
        private static bool IsPerfectSquare_BI(
            BigInteger n /* in */
            )
        {
            if (n.Sign < 0) return false;
            if (n < 2) return true;
            BigInteger r = IntegerRoot(n, 2);
            return r * r == n;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Optional AKS deterministic primality test. This is enabled
        //       via UseAksForPrimeGeneration; it is correct but far slower
        //       than the MR / BPSW path and is provided mainly for
        //       certifiable primes.
        //
        /// <summary>
        /// Deterministic AKS primality test: returns true if and only if
        /// <paramref name="n" /> is prime, with no probabilistic error.
        /// Rejects perfect powers, finds a suitable order r, checks gcd
        /// witnesses up to r, and verifies the AKS polynomial congruence (X +
        /// a)^n == X^n + a in Z_n[X] / (X^r - 1) for a range of a. Correct
        /// but very slow; intended for the rare case where a certified (not
        /// merely probable) prime is required.
        /// </summary>
        /// <param name="n">
        /// The candidate to test.
        /// </param>
        /// <returns>
        /// True if <paramref name="n" /> is prime by the deterministic AKS
        /// test; otherwise false.
        /// </returns>
        private static bool IsPrimeAKS(
            BigInteger n /* in */
            )
        {
            if (n < 2) return false;
            if (n == 2 || n == 3) return true;
            if ((n & 1) == 0) return false;
            if (IsPerfectPower(n)) return false;

            double log2n = BigInteger.Log(n, 2.0);
            long maxK = (long)Math.Ceiling(log2n * log2n);
            int r = FindSuitableR(n, maxK);

            for (int a = 2; a <= r; a++)
            {
                BigInteger g = BigInteger.GreatestCommonDivisor(
                    n, new BigInteger(a));
                if (g > BigInteger.One && g < n) return false;
            }
            if (n <= r) return true;

            int phiR = Phi(r);
            int bound = (int)(Math.Floor(Math.Sqrt(phiR) * log2n));

            if (bound < 1) bound = 1;
            for (int a = 1; a <= bound; a++)
            {
                if (!AksPolynomialCheck(n, r, a)) return false;
            }
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns true if <paramref name="n" /> is a perfect power a^b for
        /// some integers a &gt;= 2 and b in [2, 64] (the bound suffices for
        /// all key sizes used here). The AKS test rejects perfect powers up
        /// front, since they are trivially composite.
        /// </summary>
        /// <param name="n">
        /// The value to test.
        /// </param>
        /// <returns>
        /// True if <paramref name="n" /> is a perfect power a^b with b &gt;
        /// 1; otherwise false.
        /// </returns>
        private static bool IsPerfectPower(
            BigInteger n /* in */
            )
        {
            int maxExp = 64;
            for (int b = 2; b <= maxExp; b++)
            {
                BigInteger root = IntegerRoot(n, b);
                if (BigInteger.Pow(root, b) == n) return true;
            }
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the integer b-th root of <paramref name="n" /> (the floor
        /// of n^(1/b)) via binary search. Used by
        /// <see cref="IsPerfectPower" /> and
        /// <see cref="IsPerfectSquare_BI" />.
        /// </summary>
        /// <param name="n">
        /// The radicand.
        /// </param>
        /// <param name="b">
        /// The root degree.
        /// </param>
        /// <returns>
        /// The floor of the <paramref name="b" />-th root of
        /// <paramref name="n" />.
        /// </returns>
        private static BigInteger IntegerRoot(
            BigInteger n, /* in */
            int b         /* in */
            )
        {
            if (n <= 1) return n;
            int bl = BitLength(n);
            int e = (bl + b - 1) / b;
            BigInteger hi = BigInteger.One << e;
            BigInteger lo = BigInteger.One;
            while (lo < hi)
            {
                BigInteger mid = (lo + hi + 1) >> 1;
                BigInteger p = BigInteger.Pow(mid, b);
                if (p == n) return mid;
                if (p < n) lo = mid; else hi = mid - 1;
            }
            return lo;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Finds the smallest r coprime to <paramref name="n" /> such that
        /// the multiplicative order of n modulo r exceeds
        /// <paramref name="maxK" />, as required by the AKS algorithm. Throws
        /// <see cref="CryptographicException" /> if the search exceeds a
        /// sanity bound (r &gt; 1000000).
        /// </summary>
        /// <param name="n">
        /// The candidate being AKS-tested.
        /// </param>
        /// <param name="maxK">
        /// The maximum k to search.
        /// </param>
        /// <returns>
        /// The smallest suitable AKS parameter r, or a sentinel when none is
        /// found within the bound.
        /// </returns>
        private static int FindSuitableR(
            BigInteger n, /* in */
            long maxK     /* in */
            )
        {
            int r = 2;
            while (true)
            {
                if (BigInteger.GreatestCommonDivisor(n, new BigInteger(r)) ==
                    BigInteger.One)
                {
                    int phiR = Phi(r);
                    int ord = phiR;
                    int[] primes = PrimeFactors(phiR);
                    for (int i = 0; i < primes.Length; i++)
                    {
                        int p = primes[i];
                        while (ord % p == 0)
                        {
                            BigInteger t = BigInteger.ModPow(
                                n, new BigInteger(ord / p),
                                new BigInteger(r));
                            if (t == BigInteger.One) ord /= p;
                            else break;
                        }
                    }
                    if ((long)ord > maxK) return r;
                }
                r++;
                if (r > 1000000)
                    throw new CryptographicException(
                        "AKS: r search too large.");
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Euler's totient function phi(r): the count of integers in [1, r]
        /// coprime to <paramref name="r" />, computed from r's prime
        /// factorization. Used by the AKS test to bound the polynomial-check
        /// range.
        /// </summary>
        /// <param name="r">
        /// The value.
        /// </param>
        /// <returns>
        /// Euler's totient of <paramref name="r" />.
        /// </returns>
        private static int Phi(
            int r /* in */
            )
        {
            int m = r; int result = r; int p = 2;
            while (p * p <= m)
            {
                if (m % p == 0)
                {
                    while (m % p == 0) m /= p;
                    result -= result / p;
                }
                p = (p == 2) ? 3 : (p + 2);
            }
            if (m > 1) result -= result / m;
            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the distinct prime factors of <paramref name="m" /> (each
        /// listed once), by trial division. Used by
        /// <see cref="FindSuitableR" /> to compute multiplicative orders.
        /// </summary>
        /// <param name="m">
        /// The value to factor.
        /// </param>
        /// <returns>
        /// The distinct prime factors of <paramref name="m" />.
        /// </returns>
        private static int[] PrimeFactors(
            int m /* in */
            )
        {
            List<int> res = new List<int>(8);
            int x = m;
            if (x % 2 == 0) { res.Add(2); while (x % 2 == 0) x /= 2; }
            int p = 3;
            while (p * p <= x)
            {
                if (x % p == 0)
                {
                    res.Add(p);
                    while (x % p == 0) x /= p;
                }
                p += 2;
            }
            if (x > 1) res.Add(x);
            int[] arr = new int[res.Count];
            for (int i = 0; i < res.Count; i++) arr[i] = res[i];
            return arr;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Verifies the AKS polynomial congruence for a single witness
        /// <paramref name="a" />: checks that (X + a)^n is congruent to X^n +
        /// a in the ring Z_n[X] / (X^r - 1). Returns true if the congruence
        /// holds (n passes for this a), false otherwise (which proves n
        /// composite).
        /// </summary>
        /// <param name="n">
        /// The candidate being tested.
        /// </param>
        /// <param name="r">
        /// The AKS parameter r.
        /// </param>
        /// <param name="a">
        /// The polynomial constant a.
        /// </param>
        /// <returns>
        /// True if the AKS polynomial congruence holds for
        /// <paramref name="a" />; otherwise false.
        /// </returns>
        private static bool AksPolynomialCheck(
            BigInteger n, /* in */
            int r,        /* in */
            int a         /* in */
            )
        {
            BigInteger[] basePoly = new BigInteger[r];
            basePoly[0] = new BigInteger(a); basePoly[0] %= n;
            basePoly[1] = BigInteger.One % n;

            BigInteger[] em = PolyPowMod(basePoly, n, r, n);

            int idx = (int)(n % r);
            BigInteger[] rhs = new BigInteger[r];
            rhs[idx] = BigInteger.One % n;
            rhs[0] = (new BigInteger(a) % n + n) % n;

            for (int i = 0; i < r; i++)
            {
                BigInteger li = em[i] % n; if (li.Sign < 0) li += n;
                BigInteger ri = rhs[i] % n; if (ri.Sign < 0) ri += n;
                if (li != ri) return false;
            }
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Raises the polynomial <paramref name="basePoly" /> to the power
        /// <paramref name="exp" /> in the ring (Z / mod) [X] / (X^r - 1), via
        /// square-and-multiply over <see cref="PolyMulMod" />. Coefficients
        /// are reduced modulo <paramref name="mod" /> and exponents modulo r.
        /// Used by the AKS polynomial congruence check.
        /// </summary>
        /// <param name="basePoly">
        /// The base polynomial.
        /// </param>
        /// <param name="exp">
        /// The exponent.
        /// </param>
        /// <param name="r">
        /// The polynomial modulus degree (X^r - 1).
        /// </param>
        /// <param name="mod">
        /// The integer modulus.
        /// </param>
        /// <returns>
        /// <paramref name="basePoly" /> raised to <paramref name="exp" />
        /// modulo (X^r - 1, <paramref name="mod" />).
        /// </returns>
        private static BigInteger[] PolyPowMod(
            BigInteger[] basePoly, /* in */
            BigInteger exp,        /* in */
            int r,                 /* in */
            BigInteger mod         /* in */
            )
        {
            BigInteger[] result = new BigInteger[r];
            result[0] = BigInteger.One % mod;

            BigInteger[] baseCur = PolyNormalize(basePoly, r, mod);
            BigInteger e = exp;
            while (e > BigInteger.Zero)
            {
                if (!e.IsEven) result = PolyMulMod(result, baseCur, r, mod);
                baseCur = PolyMulMod(baseCur, baseCur, r, mod);
                e >>= 1;
            }
            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Multiplies two polynomials modulo (X^r - 1) with coefficients
        /// reduced modulo <paramref name="mod" /> (cyclic convolution:
        /// exponents wrap at r). Returns the product with all coefficients in
        /// [0, mod). Inner operation of <see cref="PolyPowMod" />.
        /// </summary>
        /// <param name="a">
        /// The first polynomial.
        /// </param>
        /// <param name="b">
        /// The second polynomial.
        /// </param>
        /// <param name="r">
        /// The polynomial modulus degree.
        /// </param>
        /// <param name="mod">
        /// The integer modulus.
        /// </param>
        /// <returns>
        /// The product of <paramref name="a" /> and <paramref name="b" />
        /// modulo (X^r - 1, <paramref name="mod" />).
        /// </returns>
        private static BigInteger[] PolyMulMod(
            BigInteger[] a, /* in */
            BigInteger[] b, /* in */
            int r,          /* in */
            BigInteger mod  /* in */
            )
        {
            BigInteger[] c = new BigInteger[r];
            for (int i = 0; i < r; i++)
            {
                BigInteger ai = a[i];
                if (ai.IsZero) continue;
                for (int j = 0; j < r; j++)
                {
                    if (b[j].IsZero) continue;
                    int k = i + j; if (k >= r) k -= r;
                    BigInteger prod = (ai * b[j]) % mod;
                    c[k] = (c[k] + prod) % mod;
                }
            }
            for (int i = 0; i < r; i++) if (c[i].Sign < 0) c[i] += mod;
            return c;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a copy of the length-r polynomial <paramref name="a" />
        /// with every coefficient reduced into [0, mod). Used to canonicalize
        /// the base polynomial before <see cref="PolyPowMod" />.
        /// </summary>
        /// <param name="a">
        /// The polynomial to normalize.
        /// </param>
        /// <param name="r">
        /// The polynomial modulus degree.
        /// </param>
        /// <param name="mod">
        /// The integer modulus.
        /// </param>
        /// <returns>
        /// <paramref name="a" /> reduced modulo (X^r - 1,
        /// <paramref name="mod" />).
        /// </returns>
        private static BigInteger[] PolyNormalize(
            BigInteger[] a, /* in */
            int r,          /* in */
            BigInteger mod  /* in */
            )
        {
            BigInteger[] c = new BigInteger[r];
            for (int i = 0; i < r; i++)
            {
                BigInteger t = a[i] % mod;
                if (t.Sign < 0) t += mod;
                c[i] = t;
            }
            return c;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Byte / integer conversion helpers (C# 7.3 friendly). RSA
        //       treats octet strings as unsigned big-endian integers (RFC
        //       8017 OS2IP / I2OSP); BigInteger is signed little-endian, so
        //       these helpers bridge the two representations.
        //
        /// <summary>
        /// OS2IP (RFC 8017 section 4.2): converts an unsigned big-endian octet
        /// string <paramref name="x" /> to the non-negative integer it
        /// represents.
        /// </summary>
        /// <param name="x">
        /// The octet string.
        /// </param>
        /// <returns>
        /// The non-negative integer represented by <paramref name="x" />
        /// (big-endian).
        /// </returns>
        private static BigInteger OS2IP(
            byte[] x /* in */
            )
        {
            return FromBigEndian(x);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// I2OSP (RFC 8017 section 4.1): converts the non-negative integer
        /// <paramref name="x" /> to a big-endian octet string of EXACTLY
        /// <paramref name="k" /> bytes, left-padding with zeros as needed.
        /// Throws <see cref="CryptographicException" /> if the value does not
        /// fit in k bytes (i.e. it has non-zero octets beyond the requested
        /// length).
        /// </summary>
        /// <param name="x">
        /// The non-negative integer.
        /// </param>
        /// <param name="k">
        /// The target length in bytes.
        /// </param>
        /// <returns>
        /// The <paramref name="k" />-byte big-endian encoding of
        /// <paramref name="x" />.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if <paramref name="x" /> does not fit in
        /// <paramref name="k" /> bytes.
        /// </exception>
        private static byte[] I2OSP(
            BigInteger x, /* in */
            int k         /* in */
            )
        {
            byte[] be = ToBigEndian(x, k);
            if (be.Length == k) return be;

            if (be.Length > k)
            {
                int diff = be.Length - k;
                int i = 0; while (i < diff && be[i] == 0x00) i++;
                if (i != diff)
                    throw new CryptographicException("Integer too large.");
                byte[] trimmed = new byte[k];
                Buffer.BlockCopy(be, diff, trimmed, 0, k);
                return trimmed;
            }
            byte[] padded = new byte[k];
            Buffer.BlockCopy(be, 0, padded, k - be.Length, be.Length);
            return padded;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts an unsigned big-endian byte array to a non-negative
        /// <see cref="BigInteger" />. The bytes are reversed to little-endian
        /// and a high zero byte is appended when the most-significant bit is
        /// set, so the value is never interpreted as negative
        /// two's-complement. A null or empty input yields zero.
        /// </summary>
        /// <param name="be">
        /// The big-endian unsigned bytes.
        /// </param>
        /// <returns>
        /// The non-negative integer they represent.
        /// </returns>
        private static BigInteger FromBigEndian(
            byte[] be /* in */
            )
        {
            if (be == null || be.Length == 0) return BigInteger.Zero;
            int len = be.Length;
            bool msbSet = (be[0] & 0x80) != 0;
            byte[] little = new byte[len + (msbSet ? 1 : 0)];
            for (int i = 0; i < len; i++) little[i] = be[len - 1 - i];
            if (msbSet) little[len] = 0x00; // force positive
            return new BigInteger(little);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts a non-negative <see cref="BigInteger" /> to its
        /// minimal-length unsigned big-endian byte array (the BigInteger sign
        /// byte is dropped).
        /// </summary>
        /// <param name="x">
        /// The non-negative value to convert.
        /// </param>
        /// <returns>
        /// The minimal big-endian unsigned encoding of <paramref name="x" />.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if <paramref name="x" /> is negative.
        /// </exception>
        private static byte[] ToBigEndian(
            BigInteger x /* in */
            )
        {
            return ToBigEndian(x, -1);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts a non-negative <see cref="BigInteger" /> to an unsigned
        /// big-endian byte array. With <paramref name="forceSize" /> = -1 the
        /// minimal-length encoding is returned (the BigInteger sign byte is
        /// dropped); with a non-negative <paramref name="forceSize" /> the
        /// result is exactly that many bytes, left-padded with zeros or
        /// left-trimmed of leading zeros as needed.
        /// </summary>
        /// <param name="x">
        /// The non-negative value to convert.
        /// </param>
        /// <param name="forceSize">
        /// The exact output length in bytes, or -1 for the minimal-length
        /// encoding.
        /// </param>
        /// <returns>
        /// The big-endian unsigned encoding of <paramref name="x" />.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if <paramref name="x" /> is negative, or if it does not fit
        /// in <paramref name="forceSize" /> bytes.
        /// </exception>
        private static byte[] ToBigEndian(
            BigInteger x,     /* in */
            int forceSize     /* in */
            )
        {
            if (x.Sign < 0)
                throw new CryptographicException(
                    "Negative value not supported for unsigned export.");

            // little-endian two's complement
            byte[] littleSigned = x.ToByteArray();
            int len = littleSigned.Length;
            // drop sign byte
            if (x.Sign >= 0 && len > 1 && littleSigned[len - 1] == 0x00) len--;

            byte[] be = new byte[len];
            for (int i = 0; i < len; i++) be[i] = littleSigned[len - 1 - i];

            if (forceSize < 0) return be;

            if (be.Length == forceSize) return be;
            if (be.Length > forceSize)
            {
                int extra = be.Length - forceSize;
                for (int i = 0; i < extra; i++)
                    if (be[i] != 0x00)
                        throw new CryptographicException(
                            "Value longer than requested size.");
                byte[] trimmed = new byte[forceSize];
                Buffer.BlockCopy(be, extra, trimmed, 0, forceSize);
                return trimmed;
            }
            else
            {
                byte[] padded = new byte[forceSize];
                Buffer.BlockCopy(
                    be, 0, padded, forceSize - be.Length, be.Length);
                return padded;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the number of significant bits in the non-negative integer
        /// <paramref name="x" /> (the position of its highest set bit), or 0
        /// for zero. Computed from the two's-complement byte array without
        /// allocating beyond that array.
        /// </summary>
        /// <param name="x">
        /// The value.
        /// </param>
        /// <returns>
        /// The number of significant bits in <paramref name="x" />.
        /// </returns>
        private static int BitLength(
            BigInteger x /* in */
            )
        {
            byte[] bytes = x.ToByteArray();
            if (bytes.Length == 0) return 0;
            byte b = bytes[bytes.Length - 1];
            int msb = 8;
            while (msb > 0 && ((b >> (msb - 1)) & 1) == 0) msb--;
            return (bytes.Length - 1) * 8 + msb;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Decodes a hex string of an even number of digits into the
        /// corresponding bytes. Used only for the compile-time-constant
        /// DigestInfo prefixes; not intended for untrusted input.
        /// </summary>
        /// <param name="s">
        /// The hexadecimal string.
        /// </param>
        /// <returns>
        /// The decoded bytes.
        /// </returns>
        private static byte[] Hex(
            string s /* in */
            )
        {
            int len = s.Length / 2;
            byte[] r = new byte[len];
            for (int i = 0; i < len; i++)
            {
                int hi = Convert.ToInt32(s.Substring(2 * i, 1), 16);
                int lo = Convert.ToInt32(s.Substring(2 * i + 1, 1), 16);
                r[i] = (byte)((hi << 4) | lo);
            }
            return r;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the least common multiple of <paramref name="a" /> and
        /// <paramref name="b" />, computed as |a / gcd(a, b) * b|. Used to
        /// derive the Carmichael-style modulus lambda = lcm(p-1, q-1) for the
        /// private exponent.
        /// </summary>
        /// <param name="a">
        /// The first value.
        /// </param>
        /// <param name="b">
        /// The second value.
        /// </param>
        /// <returns>
        /// The least common multiple of <paramref name="a" /> and
        /// <paramref name="b" />.
        /// </returns>
        private static BigInteger Lcm(
            BigInteger a, /* in */
            BigInteger b  /* in */
            )
        {
            BigInteger g = BigInteger.GreatestCommonDivisor(a, b);
            return BigInteger.Abs((a / g) * b);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the modular inverse of <paramref name="a" /> modulo
        /// <paramref name="n" /> via the extended Euclidean algorithm.
        /// Returns true and sets <paramref name="result" /> to a value in [0,
        /// n) when the inverse exists; returns false (with result = 0) when
        /// gcd(a, n) != 1. The blinding path in
        /// <see cref="PrivateTransform" /> uses this as a combined
        /// coprimality test and inverse computation, avoiding a separate gcd
        /// call.
        /// </summary>
        /// <param name="a">
        /// The value to invert.
        /// </param>
        /// <param name="n">
        /// The modulus.
        /// </param>
        /// <param name="result">
        /// Receives the modular inverse when it exists.
        /// </param>
        /// <returns>
        /// True if the inverse exists (<paramref name="a" /> and
        /// <paramref name="n" /> are coprime); otherwise false.
        /// </returns>
        private static bool TryModInverse(
            BigInteger a,         /* in */
            BigInteger n,         /* in */
            out BigInteger result /* out */
            )
        {
            BigInteger t = BigInteger.Zero, newT = BigInteger.One;
            BigInteger r = n, newR = a % n;
            while (newR != 0)
            {
                BigInteger q = r / newR;
                BigInteger tmp = newT; newT = t - q * newT; t = tmp;
                tmp = newR; newR = r - q * newR; r = tmp;
            }
            if (r > 1)
            {
                result = BigInteger.Zero;
                return false; // gcd(a, n) != 1 -> not invertible
            }
            if (t.Sign < 0) t += n;
            result = t;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the modular inverse of <paramref name="a" /> modulo
        /// <paramref name="n" />, throwing
        /// <see cref="CryptographicException" /> when no inverse exists
        /// (gcd(a, n) != 1). Convenience wrapper over
        /// <see cref="TryModInverse" /> for call sites where
        /// non-invertibility is a genuine error (e.g. deriving d, qInv).
        /// </summary>
        /// <param name="a">
        /// The value to invert.
        /// </param>
        /// <param name="n">
        /// The modulus.
        /// </param>
        /// <returns>
        /// The inverse of <paramref name="a" /> modulo <paramref name="n" />.
        /// </returns>
        private static BigInteger ModInverse(
            BigInteger a, /* in */
            BigInteger n  /* in */
            )
        {
            BigInteger result;
            if (!TryModInverse(a, n, out result))
                throw new CryptographicException("Inverse does not exist.");
            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a uniformly random integer in [1, 2^bits - 1] drawn from
        /// the class cryptographic RNG (never zero). Used for the small
        /// exponent-blinding factors (<see cref="EXPONENT_BLIND_BITS" />),
        /// where a full-width random would needlessly inflate the modular
        /// exponent size. Returns 1 when <paramref name="bits" /> &lt;= 0.
        /// </summary>
        /// <param name="bits">
        /// The exact bit length.
        /// </param>
        /// <returns>
        /// A random positive integer of exactly <paramref name="bits" />
        /// bits.
        /// </returns>
        private static BigInteger RandomPositiveBits(
            int bits /* in */
            )
        {
            if (bits <= 0) return BigInteger.One;
            int nbytes = (bits + 7) / 8;
            byte[] buf = new byte[nbytes];
            BigInteger x;
            do
            {
                FillBytes(buf);
                int excess = nbytes * 8 - bits;
                // big-endian top byte
                if (excess > 0) buf[0] &= (byte)(0xFF >> excess);
                x = FromBigEndian(buf);
            } while (x.IsZero);
            return x;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a uniformly random integer in [1, n-1] drawn from the
        /// class cryptographic RNG, using rejection sampling: a value the
        /// same byte length as <paramref name="n" /> is generated, its
        /// surplus high bits are masked off to match n's bit length (so
        /// rejection is efficient), and values that are zero or &gt;= n are
        /// rejected. Used for message blinding factors and CRT
        /// exponent-blinding offsets. The result is uniform and never zero.
        /// </summary>
        /// <param name="n">
        /// The exclusive upper bound.
        /// </param>
        /// <returns>
        /// A uniformly random integer in [0, <paramref name="n" />).
        /// </returns>
        private static BigInteger RandomBigIntegerBelow(
            BigInteger n /* in */
            )
        {
            byte[] be = ToBigEndian(n);
            if (be.Length == 0) return BigInteger.Zero;
            byte[] r = new byte[be.Length];
            BigInteger x;
            do
            {
                FillBytes(r);
                int msbZeros = 0; byte first = be[0];
                while (msbZeros < 8 && (first & (1 << (7 - msbZeros))) == 0)
                    msbZeros++;
                if (msbZeros > 0) r[0] &= (byte)(0xFF >> msbZeros);
                x = FromBigEndian(r);
            } while (x >= n || x.IsZero);
            return x;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns true if <paramref name="bits" /> is an allowed key size,
        /// i.e. within one of the <see cref="LegalKeySizes" /> ranges and
        /// aligned to that range's skip increment. Deliberately admits sizes
        /// outside the platform RSA limits (both smaller and larger), which
        /// is the purpose of this provider; see
        /// <see cref="MAXIMUM_KEY_SIZE" />.
        /// </summary>
        /// <param name="bits">
        /// The key size in bits to check.
        /// </param>
        /// <returns>
        /// True if <paramref name="bits" /> is within the supported range and
        /// granularity; otherwise false.
        /// </returns>
        private static bool IsLegalKeySize(
            int bits /* in */
            )
        {
            for (int i = 0; i < _legal.Length; i++)
            {
                KeySizes ks = _legal[i];
                if (bits >= ks.MinSize && bits <= ks.MaxSize &&
                    (bits - ks.MinSize) % ks.SkipSize == 0)
                    return true;
            }
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: Small low-level helpers.
        //
        /// <summary>
        /// Fills <paramref name="buffer" /> completely with cryptographically
        /// strong random bytes from the shared, thread-safe RNG. This is the
        /// single source of randomness for the whole class (key generation,
        /// padding, salts, and blinding).
        /// </summary>
        /// <param name="buffer">
        /// The buffer to fill with cryptographic random bytes.
        /// </param>
        private static void FillBytes(
            byte[] buffer /* in, out */
            )
        {
            _rng.GetBytes(buffer);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the byte-wise XOR of two equal-length arrays. Throws
        /// <see cref="ArgumentException" /> if either is null or the lengths
        /// differ. Used to mask/unmask the OAEP and PSS data blocks and seeds.
        /// </summary>
        /// <param name="a">
        /// The first buffer.
        /// </param>
        /// <param name="b">
        /// The second buffer.
        /// </param>
        /// <returns>
        /// The byte-wise XOR of <paramref name="a" /> and
        /// <paramref name="b" />.
        /// </returns>
        private static byte[] Xor(
            byte[] a, /* in */
            byte[] b  /* in */
            )
        {
            if (a == null || b == null || a.Length != b.Length)
                throw new ArgumentException("length mismatch");
            byte[] r = new byte[a.Length];
            for (int i = 0; i < r.Length; i++) r[i] = (byte)(a[i] ^ b[i]);
            return r;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Best-effort secure wipe of <paramref name="a" /> (no-op if null).
        /// See the body NOTE: delegates to the shared Utility helper that
        /// uses a non-elidable native zeroing primitive when available.
        /// Failure to wipe is treated as non-fatal.
        /// </summary>
        /// <param name="a">
        /// The buffer to overwrite with zeros.
        /// </param>
        private static void ZeroMemory(
            byte[] a /* in, out */
            )
        {
            if (a == null) return;
            //
            // NOTE: Delegate to the shared secure-zero helper, which uses a
            //       non-elidable native zeroing primitive (RtlZeroMemory /
            //       memset) when available and falls back to Array.Clear
            //       otherwise. Best-effort: a failure to wipe is non-fatal.
            //
            Result error = null;
            Utility.ZeroMemory(a, ref error);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Compares <paramref name="count" /> bytes of <paramref name="a" />
        /// (from <paramref name="aOff" />) against <paramref name="b" />
        /// (from <paramref name="bOff" />) and returns true only if they are
        /// equal. The comparison runs in time independent of WHERE the first
        /// difference is (every byte is always examined; results are
        /// OR-accumulated), so it does not leak match position via timing.
        /// Null arguments, negative offsets, or out-of-range ranges return
        /// false (these structural checks are not secret-dependent). Used for
        /// the PKCS#1 v1.5 and PSS signature comparisons.
        /// </summary>
        /// <param name="a">
        /// The first buffer.
        /// </param>
        /// <param name="aOff">
        /// The offset into <paramref name="a" />.
        /// </param>
        /// <param name="b">
        /// The second buffer.
        /// </param>
        /// <param name="bOff">
        /// The offset into <paramref name="b" />.
        /// </param>
        /// <param name="count">
        /// The number of bytes to compare.
        /// </param>
        /// <returns>
        /// True if the <paramref name="count" />-byte regions are equal; the
        /// comparison runs in constant time.
        /// </returns>
        private static bool FixedTimeEquals(
            byte[] a,  /* in */
            int aOff,  /* in */
            byte[] b,  /* in */
            int bOff,  /* in */
            int count  /* in */
            )
        {
            if (a == null || b == null) return false;
            if (aOff < 0 || bOff < 0 || count < 0) return false;
            if (aOff + count > a.Length || bOff + count > b.Length)
                return false;

            int diff = 0;
            for (int i = 0; i < count; i++) diff |= a[aOff + i] ^ b[bOff + i];
            return diff == 0;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Branchless equality for two byte-range values (0..255): returns 1
        /// if <paramref name="a" /> equals <paramref name="b" />, otherwise
        /// 0, without any data-dependent branch. Relies on the fact that, for
        /// a non-negative difference x in 0..255, (x - 1) has its sign bit
        /// set only when x == 0; an arithmetic right shift by 31 then yields
        /// all-ones for equal and zero otherwise. Building block for the
        /// constant-time padding decoders.
        /// </summary>
        /// <param name="a">
        /// The first value.
        /// </param>
        /// <param name="b">
        /// The second value.
        /// </param>
        /// <returns>
        /// 0xFF if the low bytes of <paramref name="a" /> and
        /// <paramref name="b" /> are equal; otherwise 0.
        /// </returns>
        private static int ConstantTimeByteEq(
            int a, /* in */
            int b  /* in */
            )
        {
            int x = a ^ b; // 0 if equal, non-zero otherwise
            // For x in 0..255: (x-1) is -1 when x==0, non-negative otherwise.
            // Arithmetic right shift of -1 by 31 gives -1; of non-negative
            // gives 0.
            return ((x - 1) >> 31) & 1;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts an unsigned LITTLE-endian byte array to a non-negative
        /// <see cref="BigInteger" />, appending a high zero byte when the top
        /// bit is set so the value is never read as negative. A null or empty
        /// input yields zero. Used when interpreting little-endian random
        /// buffers as magnitudes.
        /// </summary>
        /// <param name="little">
        /// The little-endian unsigned bytes.
        /// </param>
        /// <returns>
        /// The non-negative integer they represent.
        /// </returns>
        private static BigInteger FromLittleEndianUnsigned(
            byte[] little /* in */
            )
        {
            if (little == null || little.Length == 0) return BigInteger.Zero;
            int len = little.Length;
            bool msbSet = (little[len - 1] & 0x80) != 0;
            if (msbSet)
            {
                byte[] tmp = new byte[len + 1];
                Buffer.BlockCopy(little, 0, tmp, 0, len);
                tmp[len] = 0x00;
                return new BigInteger(tmp);
            }
            else
            {
                return new BigInteger(little);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: CryptoAPI (CSP) blob support.  These little-endian writers and
        //       the fixed-size converter are used only to assemble CAPI key
        //       blobs in ExportCspBlob.
        //
        /// <summary>
        /// Appends <paramref name="v" /> to <paramref name="b" /> as two
        /// little-endian bytes (low byte first), the CAPI WORD ordering.
        /// </summary>
        /// <param name="b">
        /// The byte list to append to.
        /// </param>
        /// <param name="v">
        /// The 16-bit value to append in little-endian order.
        /// </param>
        private static void AppendUInt16LE(
            List<byte> b, /* in, out */
            ushort v      /* in */
            )
        {
            b.Add((byte)(v & 0xFF));
            b.Add((byte)((v >> 8) & 0xFF));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Appends <paramref name="v" /> to <paramref name="b" /> as four
        /// little-endian bytes (low byte first), the CAPI DWORD ordering.
        /// </summary>
        /// <param name="b">
        /// The byte list to append to.
        /// </param>
        /// <param name="v">
        /// The 32-bit value to append in little-endian order.
        /// </param>
        private static void AppendUInt32LE(
            List<byte> b, /* in, out */
            uint v        /* in */
            )
        {
            b.Add((byte)(v & 0xFF));
            b.Add((byte)((v >> 8) & 0xFF));
            b.Add((byte)((v >> 16) & 0xFF));
            b.Add((byte)((v >> 24) & 0xFF));
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Converts a non-negative <see cref="BigInteger" /> to an unsigned
        /// LITTLE-endian byte array of EXACTLY <paramref name="size" /> bytes
        /// (the layout CAPI key blobs require), zero-padding the high end.
        /// Throws <see cref="CryptographicException" /> if
        /// <paramref name="x" /> is negative or does not fit in
        /// <paramref name="size" /> bytes.
        /// </summary>
        /// <param name="x">
        /// The non-negative integer.
        /// </param>
        /// <param name="size">
        /// The fixed output length in bytes.
        /// </param>
        /// <returns>
        /// The <paramref name="size" />-byte little-endian encoding of
        /// <paramref name="x" />.
        /// </returns>
        /// <exception cref="CryptographicException">
        /// Thrown if <paramref name="x" /> is negative or does not fit in
        /// <paramref name="size" /> bytes.
        /// </exception>
        private static byte[] ToLittleEndianUnsignedFixed(
            BigInteger x, /* in */
            int size      /* in */
            )
        {
            if (x.Sign < 0)
                throw new CryptographicException(
                    "Negative value not supported for unsigned export.");

            // BigInteger.ToByteArray() => little-endian two's complement
            // minimal form
            byte[] le = x.ToByteArray();
            int len = le.Length;

            // For non-negative numbers, the last byte may be a sign 0x00;
            // drop it.
            if (len > 1 && le[len - 1] == 0x00) len--;

            if (len > size)
            {
                // Only allow trimming if the extra high-order bytes are zeros
                // (shouldn't happen for minimal form).
                for (int i = size; i < len; i++)
                {
                    if (le[i] != 0x00)
                        throw new CryptographicException(
                            "Integer does not fit in requested size.");
                }
                len = size;
            }

            byte[] outLE = new byte[size];
            // copy least-significant bytes
            if (len > 0) Buffer.BlockCopy(le, 0, outLE, 0, len);
            return outLE;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region BigBigInteger Helper Class (.NET Framework 2.0 RTM or later)
        //
        // NOTE: Signed, System.Numerics.BigInteger-compatible big integer
        //       used so the RSA provider can run where System.Numerics is
        //       unavailable (.NET 2.0). value = _sign * magnitude(_limbs);
        //       _limbs is the trimmed little-endian magnitude (empty == zero)
        //       and _sign is in {-1, 0, +1}. The constant- time CIOS
        //       Montgomery, NTT multiply, and Barrett+NTT modexp internals
        //       operate on magnitudes; a signed layer wraps them. Authored
        //       to the C# 2.0 language level and the .NET 2.0 BCL.
        //
        /// <summary>
        /// A self-contained, signed big integer that is API-compatible with
        /// <see cref="System.Numerics.BigInteger" />, used so the RSA
        /// provider can run where System.Numerics is unavailable (.NET
        /// Framework 2.0 - 3.5). The value is _sign * magnitude(_limbs):
        /// _limbs is the trimmed little-endian base-2^32 magnitude (empty
        /// means zero) and _sign is -1, 0, or +1. The constant-time CIOS
        /// Montgomery multiply, the NTT multiply, and the Barrett+NTT modular
        /// exponentiation operate on magnitudes; a signed layer wraps them.
        /// Authored to the C# 2.0 language level and the .NET 2.0 BCL.
        /// </summary>
        [ObjectId("1f3a6b2c-7d84-4e95-a0b1-c2d3e4f50617")]
        internal sealed class BigBigInteger
        {
            #region Private Constants
            //
            // NOTE: Crossover (in limbs) above which the NTT multiply is
            //       used instead of the schoolbook multiply.
            //
            /// <summary>
            /// Crossover, in 32-bit limbs, at or above which the NTT-based
            /// multiply is used instead of the schoolbook multiply.
            /// </summary>
            private const int NttThresholdLimbs = 1792;

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: At/above this modulus limb count, ModPow uses the
            //       Barrett+NTT path instead of CIOS Montgomery.
            //
            /// <summary>
            /// Modulus size, in 32-bit limbs, at or above which
            /// <see cref="ModPow" /> uses the Barrett+NTT exponentiation path
            /// instead of CIOS Montgomery.
            /// </summary>
            private const int LargeBarrettThresholdLimbs = 2048;

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Two-prime NTT parameters: primes < 2^31 of the form
            //       k*2^e+1 and their primitive roots, used by the NTT
            //       convolution multiply (recombined via CRT).
            //
            /// <summary>
            /// First NTT prime (a 31-bit prime of the form k*2^e+1) used by
            /// the two-prime NTT convolution multiply.
            /// </summary>
            private const long NttP1 = 2013265921; // 15*2^27 + 1
            /// <summary>
            /// Second NTT prime (a 31-bit prime of the form k*2^e+1) used by
            /// the two-prime NTT convolution multiply.
            /// </summary>
            private const long NttP2 = 1811939329; // 27*2^26 + 1
            /// <summary>
            /// Primitive root of <see cref="NttP1" /> used as the NTT
            /// transform root.
            /// </summary>
            private const long NttG1 = 31; // primitive root of NttP1
            /// <summary>
            /// Primitive root of <see cref="NttP2" /> used as the NTT
            /// transform root.
            /// </summary>
            private const long NttG2 = 13; // primitive root of NttP2
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Static Data
            /// <summary>
            /// The value zero (the canonical empty-magnitude instance).
            /// </summary>
            public static readonly BigBigInteger Zero = new BigBigInteger();

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The value one.
            /// </summary>
            public static readonly BigBigInteger One =
                Make(new uint[] { 1u }, 1);
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Static Data
            /// <summary>
            /// Shared empty limb array used as the magnitude of zero.
            /// </summary>
            private static readonly uint[] EmptyLimbs = new uint[0];

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Shared single-limb magnitude array representing the value one.
            /// </summary>
            private static readonly uint[] OneMag = new uint[] { 1u };
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Data
            /// <summary>
            /// Little-endian magnitude limbs (base 2^32), trimmed of
            /// leading-zero limbs; empty when the value is zero.
            /// </summary>
            private uint[] _limbs;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Sign of the value: -1, 0, or +1 (zero only when the magnitude
            /// is empty).
            /// </summary>
            private int _sign;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Constructors
            /// <summary>
            /// Creates a zero value backed by the shared empty magnitude.
            /// </summary>
            private BigBigInteger()
            {
                _limbs = EmptyLimbs;
                _sign = 0;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Creates a value from a 32-bit signed integer.
            /// </summary>
            /// <param name="v">
            /// The integer value.
            /// </param>
            public BigBigInteger(
                int v /* in */
                )
            {
                InitFromLong(v);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Creates a value from a 64-bit signed integer.
            /// </summary>
            /// <param name="v">
            /// The integer value.
            /// </param>
            public BigBigInteger(
                long v /* in */
                )
            {
                InitFromLong(v);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Creates a value from a 32-bit unsigned integer.
            /// </summary>
            /// <param name="v">
            /// The unsigned integer value.
            /// </param>
            public BigBigInteger(
                uint v /* in */
                )
            {
                if (v == 0)
                {
                    _limbs = EmptyLimbs;
                    _sign = 0;
                }
                else
                {
                    _limbs = new uint[] { v };
                    _sign = 1;
                }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Creates a value from its little-endian two's-complement byte
            /// representation (the same layout as
            /// <see cref="System.Numerics.BigInteger" />).
            /// </summary>
            /// <param name="value">
            /// The little-endian two's-complement bytes.
            /// </param>
            public BigBigInteger(
                byte[] value /* in */
                )
            {
                InitFromLeTwosComplement(value);
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Properties
            /// <summary>
            /// Gets a value indicating whether this value is zero.
            /// </summary>
            /// <returns>
            /// True if this value is zero; otherwise false.
            /// </returns>
            public bool IsZero
            {
                get { return _sign == 0; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets a value indicating whether this value is one.
            /// </summary>
            /// <returns>
            /// True if this value is one; otherwise false.
            /// </returns>
            public bool IsOne
            {
                get
                {
                    return _sign == 1 && _limbs.Length == 1 &&
                        _limbs[0] == 1u;
                }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets the sign of this value.
            /// </summary>
            /// <returns>
            /// -1 if negative, 0 if zero, or +1 if positive.
            /// </returns>
            public int Sign
            {
                get { return _sign; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets a value indicating whether this value is even.
            /// </summary>
            /// <returns>
            /// True if this value is even; otherwise false.
            /// </returns>
            public bool IsEven
            {
                get { return _sign == 0 || (_limbs[0] & 1u) == 0u; }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Methods
            //
            // NOTE: Best-effort wipe of this value's backing store. Callers
            //       holding secret values (e.g. a private exponent) should
            //       Zeroize() when done.
            //
            /// <summary>
            /// Performs a best-effort wipe of this value's backing magnitude.
            /// Callers holding secret values (e.g. a private exponent) should
            /// call this when done.
            /// </summary>
            public void Zeroize()
            {
                if (_limbs.Length != 0)
                    Array.Clear(_limbs, 0, _limbs.Length);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Little-endian two's-complement serialization,
            //       byte-for-byte identical to
            //       System.Numerics.BigInteger.ToByteArray().
            //
            /// <summary>
            /// Returns the little-endian two's-complement byte representation
            /// of this value, identical to
            /// <see cref="System.Numerics.BigInteger.ToByteArray()" />.
            /// </summary>
            /// <returns>
            /// The little-endian two's-complement bytes.
            /// </returns>
            public byte[] ToByteArray()
            {
                if (_sign == 0) return new byte[] { 0 };

                byte[] mag = MagToLeBytesMinimal(_limbs);

                if (_sign > 0)
                {
                    if ((mag[mag.Length - 1] & 0x80) != 0)
                    {
                        byte[] t = new byte[mag.Length + 1];

                        Array.Copy(mag, t, mag.Length);
                        return t;
                    }

                    return mag;
                }

                byte[] tc = new byte[mag.Length];
                int carry = 1;

                for (int i = 0; i < mag.Length; i++)
                {
                    int x = (mag[i] ^ 0xFF) + carry;

                    tc[i] = (byte)x;
                    carry = x >> 8;
                }

                if ((tc[tc.Length - 1] & 0x80) == 0)
                {
                    byte[] t = new byte[tc.Length + 1];

                    Array.Copy(tc, t, tc.Length);
                    t[tc.Length] = 0xFF;
                    return t;
                }

                return tc;
            }

            ///////////////////////////////////////////////////////////////////

#if HAVE_SYSTEM_NUMERICS
            /// <summary>
            /// Converts this value to a framework
            /// <see cref="System.Numerics.BigInteger" /> (compiled only where
            /// that type exists).
            /// </summary>
            /// <returns>
            /// The equivalent <see cref="System.Numerics.BigInteger" />.
            /// </returns>
            public System.Numerics.BigInteger ToBigInteger()
            {
                return new System.Numerics.BigInteger(ToByteArray());
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Static Methods
            /// <summary>
            /// Creates a value from a 64-bit signed integer.
            /// </summary>
            /// <param name="v">
            /// The integer value.
            /// </param>
            /// <returns>
            /// The corresponding <see cref="BigBigInteger" />.
            /// </returns>
            public static BigBigInteger FromInt64(
                long v /* in */
                )
            {
                return new BigBigInteger(v);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Creates a value from a 32-bit unsigned integer.
            /// </summary>
            /// <param name="v">
            /// The unsigned integer value.
            /// </param>
            /// <returns>
            /// The corresponding <see cref="BigBigInteger" />.
            /// </returns>
            public static BigBigInteger FromUInt32(
                uint v /* in */
                )
            {
                return new BigBigInteger(v);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Compares two values.
            /// </summary>
            /// <param name="a">
            /// The first value.
            /// </param>
            /// <param name="b">
            /// The second value.
            /// </param>
            /// <returns>
            /// A negative number, zero, or a positive number according to
            /// whether <paramref name="a" /> is less than, equal to, or
            /// greater than <paramref name="b" />.
            /// </returns>
            public static int Compare(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                if (a._sign != b._sign) return a._sign < b._sign ? -1 : 1;
                if (a._sign == 0) return 0;

                int c = CompareMag(
                    a._limbs, a._limbs.Length, b._limbs, b._limbs.Length);

                return a._sign > 0 ? c : -c;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns the absolute value of <paramref name="a" />.
            /// </summary>
            /// <param name="a">
            /// The value.
            /// </param>
            /// <returns>
            /// The absolute value.
            /// </returns>
            public static BigBigInteger Abs(
                BigBigInteger a /* in */
                )
            {
                return Make(a._limbs, a._sign == 0 ? 0 : 1);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Raises <paramref name="value" /> to a non-negative integer
            /// power.
            /// </summary>
            /// <param name="value">
            /// The base value.
            /// </param>
            /// <param name="exponent">
            /// The non-negative exponent.
            /// </param>
            /// <returns>
            /// <paramref name="value" /> raised to
            /// <paramref name="exponent" />.
            /// </returns>
            /// <exception cref="ArgumentOutOfRangeException">
            /// Thrown if <paramref name="exponent" /> is negative.
            /// </exception>
            public static BigBigInteger Pow(
                BigBigInteger value, /* in */
                int exponent         /* in */
                )
            {
                if (exponent < 0)
                    throw new ArgumentOutOfRangeException("exponent");

                BigBigInteger result = One;
                BigBigInteger b = value;
                int e = exponent;

                while (e > 0)
                {
                    if ((e & 1) == 1) result = result * b;
                    e >>= 1;
                    if (e > 0) b = b * b;
                }

                return result;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns the greatest common divisor of two values.
            /// </summary>
            /// <param name="a">
            /// The first value.
            /// </param>
            /// <param name="b">
            /// The second value.
            /// </param>
            /// <returns>
            /// The greatest common divisor of <paramref name="a" /> and
            /// <paramref name="b" />.
            /// </returns>
            public static BigBigInteger GreatestCommonDivisor(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                uint[] x = (uint[])a._limbs.Clone();
                uint[] y = (uint[])b._limbs.Clone();

                while (y.Length != 0)
                {
                    uint[] q, r;

                    DivRemMag(x, y, out q, out r);
                    x = y;
                    y = r;
                }

                return Make(x, x.Length == 0 ? 0 : 1);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Approximate logarithm sufficient for the (opt-in) AKS
            //       path: ln(value) is computed from the top 64 bits and the
            //       bit length.
            //
            /// <summary>
            /// Returns an approximation of the logarithm of
            /// <paramref name="value" /> in the given base, computed from the
            /// top bits and the bit length (sufficient for the opt-in AKS
            /// path).
            /// </summary>
            /// <param name="value">
            /// The value whose logarithm is computed.
            /// </param>
            /// <param name="baseValue">
            /// The logarithm base.
            /// </param>
            /// <returns>
            /// An approximation of log(<paramref name="value" />) in base
            /// <paramref name="baseValue" />.
            /// </returns>
            public static double Log(
                BigBigInteger value, /* in */
                double baseValue     /* in */
                )
            {
                if (value._sign <= 0) return double.NaN;

                int bl = BitLengthLimbs(value._limbs);
                ulong top;

                if (bl <= 64)
                {
                    top = value._limbs[0];
                    if (value._limbs.Length > 1)
                        top |= (ulong)value._limbs[1] << 32;
                }
                else
                {
                    uint[] sh = ShiftRightMag(value._limbs, bl - 64);

                    top = sh[0];
                    if (sh.Length > 1) top |= (ulong)sh[1] << 32;
                }

                double lnValue = Math.Log((double)top) +
                    (double)(bl - 64) * Math.Log(2.0);

                return lnValue / Math.Log(baseValue);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: ModPow assumes non-negative value/exponent and an odd
            //       modulus (the RSA usage). It dispatches the large-modulus
            //       Barrett+NTT path and the small/medium CIOS Montgomery
            //       path.
            //
            /// <summary>
            /// Raises <paramref name="value" /> to
            /// <paramref name="exponent" /> modulo
            /// <paramref name="modulus" />. This assumes a non-negative value
            /// and exponent and an odd modulus (the RSA usage); it dispatches
            /// the large-modulus Barrett+NTT path
            /// (<see cref="ModPowBarrettNtt" />) and the small/medium
            /// constant-time CIOS Montgomery path
            /// (<see cref="ModPowCios" />).
            /// </summary>
            /// <param name="value">
            /// The base value.
            /// </param>
            /// <param name="exponent">
            /// The exponent.
            /// </param>
            /// <param name="modulus">
            /// The odd modulus.
            /// </param>
            /// <returns>
            /// <paramref name="value" /> raised to
            /// <paramref name="exponent" /> modulo
            /// <paramref name="modulus" />.
            /// </returns>
            public static BigBigInteger ModPow(
                BigBigInteger value,    /* in */
                BigBigInteger exponent, /* in */
                BigBigInteger modulus   /* in */
                )
            {
                return ModPowCore(value, exponent, modulus, -1);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Core modular-exponentiation dispatch shared by
            /// <see cref="ModPow" />: it selects the Barrett+NTT or CIOS
            /// Montgomery path by modulus size and honors an optional fixed
            /// exponent-bit width for constant-time scheduling.
            /// </summary>
            /// <param name="value">
            /// The base value.
            /// </param>
            /// <param name="exponent">
            /// The exponent.
            /// </param>
            /// <param name="modulus">
            /// The odd modulus.
            /// </param>
            /// <param name="fixedExpBits">
            /// The fixed exponent bit width to assume, or a negative value to
            /// use the exponent's actual bit length.
            /// </param>
            /// <returns>
            /// <paramref name="value" /> raised to
            /// <paramref name="exponent" /> modulo
            /// <paramref name="modulus" />.
            /// </returns>
            public static BigBigInteger ModPowCore(
                BigBigInteger value,    /* in */
                BigBigInteger exponent, /* in */
                BigBigInteger modulus,  /* in */
                int fixedExpBits        /* in */
                )
            {
                uint[] m = modulus._limbs;
                int n = m.Length;

                if (n == 0) throw new DivideByZeroException();

                if ((m[0] & 1u) == 0)
                    throw new ArgumentException(
                        "Montgomery requires an odd modulus.");

                if (n == 1 && m[0] == 1u) return Zero;

                if (n >= LargeBarrettThresholdLimbs)
                    return ModPowBarrettNtt(
                        value, exponent, modulus, fixedExpBits);

                return ModPowCios(value, exponent, modulus, fixedExpBits);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: CIOS Montgomery fixed-window modexp (O(n^2) per multiply);
            //       the constant-time path for small/medium moduli.
            //
            /// <summary>
            /// Constant-time modular exponentiation using CIOS Montgomery
            /// multiplication with a fixed-window schedule and constant-time
            /// table selection, for small and medium odd moduli.
            /// </summary>
            /// <param name="value">
            /// The base value.
            /// </param>
            /// <param name="exponent">
            /// The exponent.
            /// </param>
            /// <param name="modulus">
            /// The odd modulus.
            /// </param>
            /// <param name="fixedExpBits">
            /// The fixed exponent bit width to assume, or a negative value to
            /// use the exponent's actual bit length.
            /// </param>
            /// <returns>
            /// <paramref name="value" /> raised to
            /// <paramref name="exponent" /> modulo
            /// <paramref name="modulus" />.
            /// </returns>
            public static BigBigInteger ModPowCios(
                BigBigInteger value,    /* in */
                BigBigInteger exponent, /* in */
                BigBigInteger modulus,  /* in */
                int fixedExpBits        /* in */
                )
            {
                uint[] m = modulus._limbs;
                int n = m.Length;

                if (n == 0) throw new DivideByZeroException();

                if ((m[0] & 1u) == 0)
                    throw new ArgumentException(
                        "Montgomery requires an odd modulus.");

                if (n == 1 && m[0] == 1u) return Zero;

                uint mp = ComputeNPrime(m[0]);
                uint[] rr = ComputeRR(m, n);
                uint[] baseRed = Mod(value._limbs, m);
                uint[] basePad = Pad(baseRed, n);
                uint[] one = new uint[n]; one[0] = 1u;
                uint[] montOne = MontMul(one, rr, m, mp, n);
                uint[] baseMont = MontMul(basePad, rr, m, mp, n);
                int w = (n * 32) <= 512 ? 4 : 5;
                int tableSize = 1 << w;
                uint[][] table = new uint[tableSize][];

                table[0] = montOne;

                for (int i = 1; i < tableSize; i++)
                    table[i] = MontMul(table[i - 1], baseMont, m, mp, n);

                uint[] exp = exponent._limbs;
                int realBits = BitLengthLimbs(exp);
                int bits = fixedExpBits >= 0 ? fixedExpBits : realBits;
                int windows = (bits + w - 1) / w;
                uint[] acc = (uint[])montOne.Clone();

                for (int wi = windows - 1; wi >= 0; wi--)
                {
                    for (int s = 0; s < w; s++)
                        acc = MontMul(acc, acc, m, mp, n);

                    int d = GetBits(exp, wi * w, w);
                    uint[] sel = CtSelect(table, d, n, tableSize);

                    acc = MontMul(acc, sel, m, mp, n);
                    Array.Clear(sel, 0, sel.Length);
                }

                uint[] outLimbs = MontMul(acc, one, m, mp, n);
                BigBigInteger ret = Make(Trim(outLimbs), 1);

                Array.Clear(baseRed, 0, baseRed.Length);
                Array.Clear(basePad, 0, basePad.Length);
                Array.Clear(baseMont, 0, baseMont.Length);
                Array.Clear(acc, 0, acc.Length);
                Array.Clear(rr, 0, rr.Length);

                for (int i = 0; i < tableSize; i++)
                    Array.Clear(table[i], 0, table[i].Length);

                return ret;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Simple (non-constant-time) square-and-multiply modular
            /// exponentiation, used to cross-check the constant-time path.
            /// </summary>
            /// <param name="value">
            /// The base value.
            /// </param>
            /// <param name="exponent">
            /// The exponent.
            /// </param>
            /// <param name="modulus">
            /// The modulus.
            /// </param>
            /// <returns>
            /// <paramref name="value" /> raised to
            /// <paramref name="exponent" /> modulo
            /// <paramref name="modulus" />.
            /// </returns>
            public static BigBigInteger ModPowSimple(
                BigBigInteger value,    /* in */
                BigBigInteger exponent, /* in */
                BigBigInteger modulus   /* in */
                )
            {
                uint[] m = modulus._limbs;

                if (m.Length == 0) throw new DivideByZeroException();
                if (m.Length == 1 && m[0] == 1u) return Zero;

                uint[] result = new uint[] { 1u };
                uint[] basev = Mod(value._limbs, m);
                uint[] e = exponent._limbs;
                int bits = BitLengthLimbs(e);

                for (int i = 0; i < bits; i++)
                {
                    if (GetBits(e, i, 1) == 1)
                        result = Mod(MulMag(result, basev), m);

                    basev = Mod(MulMag(basev, basev), m);
                }

                return Make(Trim(result), result.Length == 0 ? 0 : 1);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: NTT-based modexp -- each modular multiply is an NTT
            //       multiply followed by Barrett reduction (two more NTT
            //       multiplies), giving ~O(n^2 log n) overall versus CIOS's
            //       ~O(n^3). The fixed-window schedule, constant-time table
            //       select, and mask-select corrections preserve the same CT
            //       discipline.
            //
            /// <summary>
            /// Modular exponentiation for large odd moduli: each modular
            /// multiply is an NTT multiply followed by a Barrett reduction,
            /// with the same fixed-window schedule and constant-time table
            /// selection as the CIOS path.
            /// </summary>
            /// <param name="value">
            /// The base value.
            /// </param>
            /// <param name="exponent">
            /// The exponent.
            /// </param>
            /// <param name="modulus">
            /// The odd modulus.
            /// </param>
            /// <param name="fixedExpBits">
            /// The fixed exponent bit width to assume, or a negative value to
            /// use the exponent's actual bit length.
            /// </param>
            /// <returns>
            /// <paramref name="value" /> raised to
            /// <paramref name="exponent" /> modulo
            /// <paramref name="modulus" />.
            /// </returns>
            public static BigBigInteger ModPowBarrettNtt(
                BigBigInteger value,    /* in */
                BigBigInteger exponent, /* in */
                BigBigInteger modulus,  /* in */
                int fixedExpBits        /* in */
                )
            {
                uint[] m = modulus._limbs;
                int n = m.Length;
                uint[] mu = ComputeBarrettMu(m, n);
                uint[] baseRed = BarrettReduce(value._limbs, m, mu, n);
                int w = 4;
                int tableSize = 1 << w;
                uint[][] table = new uint[tableSize][];

                table[0] = new uint[] { 1u };

                for (int i = 1; i < tableSize; i++)
                {
                    table[i] = BarrettReduce(
                        MulMag(table[i - 1], baseRed), m, mu, n);
                }

                uint[] exp = exponent._limbs;
                int bits = fixedExpBits >= 0
                    ? fixedExpBits : BitLengthLimbs(exp);
                int windows = (bits + w - 1) / w;
                uint[] acc = new uint[] { 1u };

                for (int wi = windows - 1; wi >= 0; wi--)
                {
                    for (int s = 0; s < w; s++)
                        acc = BarrettReduce(MulMag(acc, acc), m, mu, n);

                    int d = GetBits(exp, wi * w, w);
                    uint[] sel = CtSelectVar(table, d, tableSize);

                    acc = BarrettReduce(MulMag(acc, sel), m, mu, n);

                    if (sel.Length != 0) Array.Clear(sel, 0, sel.Length);
                }

                BigBigInteger ret = Make(Trim(acc), acc.Length == 0 ? 0 : 1);

                for (int i = 0; i < tableSize; i++)
                {
                    if (table[i].Length != 0)
                        Array.Clear(table[i], 0, table[i].Length);
                }

                if (baseRed.Length != 0)
                    Array.Clear(baseRed, 0, baseRed.Length);

                return ret;
            }

            ///////////////////////////////////////////////////////////////////

#if HAVE_SYSTEM_NUMERICS
            //
            // NOTE: Bridge to the framework type, compiled only where it
            //       exists (.NET 4.0+ / .NET Standard). On .NET 2.0/3.5 this
            //       whole region is absent and BigBigInteger is the
            //       BigInteger (via the file alias).
            //
            /// <summary>
            /// Creates a value from a framework
            /// <see cref="System.Numerics.BigInteger" /> (compiled only where
            /// that type exists).
            /// </summary>
            /// <param name="v">
            /// The framework integer value.
            /// </param>
            /// <returns>
            /// The corresponding <see cref="BigBigInteger" />.
            /// </returns>
            public static BigBigInteger FromBigInteger(
                System.Numerics.BigInteger v /* in */
                )
            {
                return new BigBigInteger(v.ToByteArray());
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Operators
            /// <summary>
            /// Adds two values.
            /// </summary>
            /// <param name="a">
            /// The first addend.
            /// </param>
            /// <param name="b">
            /// The second addend.
            /// </param>
            /// <returns>
            /// The sum of <paramref name="a" /> and <paramref name="b" />.
            /// </returns>
            public static BigBigInteger operator +(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                if (a._sign == 0) return b;
                if (b._sign == 0) return a;

                if (a._sign == b._sign)
                    return Make(AddMag(a._limbs, b._limbs), a._sign);

                int c = CompareMag(
                    a._limbs, a._limbs.Length, b._limbs, b._limbs.Length);

                if (c == 0) return Zero;
                if (c > 0) return Make(SubMag(a._limbs, b._limbs), a._sign);

                return Make(SubMag(b._limbs, a._limbs), b._sign);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns the negation of <paramref name="a" />.
            /// </summary>
            /// <param name="a">
            /// The value to negate.
            /// </param>
            /// <returns>
            /// The negated value.
            /// </returns>
            public static BigBigInteger operator -(
                BigBigInteger a /* in */
                )
            {
                return Make(a._limbs, -a._sign);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Subtracts <paramref name="b" /> from <paramref name="a" />.
            /// </summary>
            /// <param name="a">
            /// The minuend.
            /// </param>
            /// <param name="b">
            /// The subtrahend.
            /// </param>
            /// <returns>
            /// The difference <paramref name="a" /> - <paramref name="b" />.
            /// </returns>
            public static BigBigInteger operator -(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                return a + (-b);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Multiplies two values.
            /// </summary>
            /// <param name="a">
            /// The first factor.
            /// </param>
            /// <param name="b">
            /// The second factor.
            /// </param>
            /// <returns>
            /// The product of <paramref name="a" /> and
            /// <paramref name="b" />.
            /// </returns>
            public static BigBigInteger operator *(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                if (a._sign == 0 || b._sign == 0) return Zero;

                return Make(MulMag(a._limbs, b._limbs), a._sign * b._sign);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Division truncates toward zero and the remainder takes
            //       the sign of the dividend, matching
            //       System.Numerics.BigInteger.
            //
            /// <summary>
            /// Divides <paramref name="a" /> by <paramref name="b" />,
            /// truncating the quotient toward zero.
            /// </summary>
            /// <param name="a">
            /// The dividend.
            /// </param>
            /// <param name="b">
            /// The divisor.
            /// </param>
            /// <returns>
            /// The truncated quotient.
            /// </returns>
            public static BigBigInteger operator /(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                uint[] q, r;

                DivRemMag(a._limbs, b._limbs, out q, out r);
                return Make(q, a._sign * b._sign);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns the remainder of dividing <paramref name="a" /> by
            /// <paramref name="b" />; the remainder takes the sign of the
            /// dividend (truncated division).
            /// </summary>
            /// <param name="a">
            /// The dividend.
            /// </param>
            /// <param name="b">
            /// The divisor.
            /// </param>
            /// <returns>
            /// The remainder.
            /// </returns>
            public static BigBigInteger operator %(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                uint[] q, r;

                DivRemMag(a._limbs, b._limbs, out q, out r);
                return Make(r, a._sign);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Shifts <paramref name="a" /> left by <paramref name="n" />
            /// bits.
            /// </summary>
            /// <param name="a">
            /// The value to shift.
            /// </param>
            /// <param name="n">
            /// The number of bits.
            /// </param>
            /// <returns>
            /// <paramref name="a" /> multiplied by 2^<paramref name="n" />.
            /// </returns>
            public static BigBigInteger operator <<(
                BigBigInteger a, /* in */
                int n            /* in */
                )
            {
                if (n == 0 || a._sign == 0) return a;
                if (n < 0) return a >> (-n);

                return Make(ShiftLeftMag(a._limbs, n), a._sign);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Arithmetic right shift == floor(value / 2^n). For
            //       negatives this rounds toward negative infinity, so add
            //       one to the shifted magnitude whenever a set bit was
            //       shifted out.
            //
            /// <summary>
            /// Arithmetic right shift of <paramref name="a" /> by
            /// <paramref name="n" /> bits (floor division by 2^n, matching
            /// <see cref="System.Numerics.BigInteger" /> for negative
            /// values).
            /// </summary>
            /// <param name="a">
            /// The value to shift.
            /// </param>
            /// <param name="n">
            /// The number of bits.
            /// </param>
            /// <returns>
            /// <paramref name="a" /> shifted right by <paramref name="n" />
            /// bits.
            /// </returns>
            public static BigBigInteger operator >>(
                BigBigInteger a, /* in */
                int n            /* in */
                )
            {
                if (n == 0 || a._sign == 0) return a;
                if (n < 0) return a << (-n);

                uint[] sh = ShiftRightMag(a._limbs, n);

                if (a._sign < 0 && AnyLowBitsSet(a._limbs, n))
                    sh = AddMag(sh, OneMag);

                return Make(sh, a._sign);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Two's-complement bitwise AND via the byte view, matching
            //       System.Numerics.BigInteger semantics for negative
            //       operands.
            //
            /// <summary>
            /// Computes the bitwise AND of two values using their
            /// two's-complement representations.
            /// </summary>
            /// <param name="a">
            /// The first operand.
            /// </param>
            /// <param name="b">
            /// The second operand.
            /// </param>
            /// <returns>
            /// The bitwise AND of <paramref name="a" /> and
            /// <paramref name="b" />.
            /// </returns>
            public static BigBigInteger operator &(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                byte[] ab = a.ToByteArray();
                byte[] bb = b.ToByteArray();
                byte aExt = (byte)(a._sign < 0 ? 0xFF : 0x00);
                byte bExt = (byte)(b._sign < 0 ? 0xFF : 0x00);
                int len = ab.Length > bb.Length ? ab.Length : bb.Length;
                byte[] res = new byte[len];

                for (int i = 0; i < len; i++)
                {
                    byte x = i < ab.Length ? ab[i] : aExt;
                    byte y = i < bb.Length ? bb[i] : bExt;

                    res[i] = (byte)(x & y);
                }

                return new BigBigInteger(res);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Determines whether <paramref name="a" /> is less than
            /// <paramref name="b" />.
            /// </summary>
            /// <param name="a">
            /// The first value.
            /// </param>
            /// <param name="b">
            /// The second value.
            /// </param>
            /// <returns>
            /// True if <paramref name="a" /> is less than
            /// <paramref name="b" />; otherwise false.
            /// </returns>
            public static bool operator <(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                return Compare(a, b) < 0;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Determines whether <paramref name="a" /> is greater than
            /// <paramref name="b" />.
            /// </summary>
            /// <param name="a">
            /// The first value.
            /// </param>
            /// <param name="b">
            /// The second value.
            /// </param>
            /// <returns>
            /// True if <paramref name="a" /> is greater than
            /// <paramref name="b" />; otherwise false.
            /// </returns>
            public static bool operator >(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                return Compare(a, b) > 0;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Determines whether <paramref name="a" /> is less than or equal
            /// to <paramref name="b" />.
            /// </summary>
            /// <param name="a">
            /// The first value.
            /// </param>
            /// <param name="b">
            /// The second value.
            /// </param>
            /// <returns>
            /// True if <paramref name="a" /> is less than or equal to
            /// <paramref name="b" />; otherwise false.
            /// </returns>
            public static bool operator <=(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                return Compare(a, b) <= 0;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Determines whether <paramref name="a" /> is greater than or
            /// equal to <paramref name="b" />.
            /// </summary>
            /// <param name="a">
            /// The first value.
            /// </param>
            /// <param name="b">
            /// The second value.
            /// </param>
            /// <returns>
            /// True if <paramref name="a" /> is greater than or equal to
            /// <paramref name="b" />; otherwise false.
            /// </returns>
            public static bool operator >=(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                return Compare(a, b) >= 0;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Determines whether two values are numerically equal.
            /// </summary>
            /// <param name="a">
            /// The first value.
            /// </param>
            /// <param name="b">
            /// The second value.
            /// </param>
            /// <returns>
            /// True if the two values are equal; otherwise false.
            /// </returns>
            public static bool operator ==(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                if (ReferenceEquals(a, b)) return true;
                if (ReferenceEquals(a, null) || ReferenceEquals(b, null))
                    return false;

                return Compare(a, b) == 0;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Determines whether two values are not numerically equal.
            /// </summary>
            /// <param name="a">
            /// The first value.
            /// </param>
            /// <param name="b">
            /// The second value.
            /// </param>
            /// <returns>
            /// True if the two values differ; otherwise false.
            /// </returns>
            public static bool operator !=(
                BigBigInteger a, /* in */
                BigBigInteger b  /* in */
                )
            {
                return !(a == b);
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Conversion Operators
            //
            // NOTE: Narrowing conversions mirror System.Numerics.BigInteger:
            //       they throw OverflowException when the value does not fit.
            //
            /// <summary>
            /// Converts a value to a 32-bit signed integer.
            /// </summary>
            /// <param name="v">
            /// The value to convert.
            /// </param>
            /// <returns>
            /// The 32-bit signed integer value.
            /// </returns>
            /// <exception cref="OverflowException">
            /// Thrown if the value does not fit in an
            /// <see cref="System.Int32" />.
            /// </exception>
            public static explicit operator int(
                BigBigInteger v /* in */
                )
            {
                if (v._sign == 0) return 0;
                if (v._limbs.Length > 1) throw new OverflowException();

                uint lo = v._limbs[0];

                if (v._sign > 0)
                {
                    if (lo > (uint)int.MaxValue) throw new OverflowException();
                    return (int)lo;
                }

                if (lo > 0x80000000u) throw new OverflowException();
                return (int)(-(long)lo);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Converts a value to a 32-bit unsigned integer.
            /// </summary>
            /// <param name="v">
            /// The value to convert.
            /// </param>
            /// <returns>
            /// The 32-bit unsigned integer value.
            /// </returns>
            /// <exception cref="OverflowException">
            /// Thrown if the value does not fit in a
            /// <see cref="System.UInt32" />.
            /// </exception>
            public static explicit operator uint(
                BigBigInteger v /* in */
                )
            {
                if (v._sign == 0) return 0u;
                if (v._sign < 0 || v._limbs.Length > 1)
                    throw new OverflowException();

                return v._limbs[0];
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Converts a value to a 64-bit signed integer.
            /// </summary>
            /// <param name="v">
            /// The value to convert.
            /// </param>
            /// <returns>
            /// The 64-bit signed integer value.
            /// </returns>
            /// <exception cref="OverflowException">
            /// Thrown if the value does not fit in an
            /// <see cref="System.Int64" />.
            /// </exception>
            public static explicit operator long(
                BigBigInteger v /* in */
                )
            {
                if (v._sign == 0) return 0L;
                if (v._limbs.Length > 2) throw new OverflowException();

                ulong lo = v._limbs[0];

                if (v._limbs.Length == 2) lo |= (ulong)v._limbs[1] << 32;

                if (v._sign > 0)
                {
                    if (lo > (ulong)long.MaxValue)
                        throw new OverflowException();
                    return (long)lo;
                }

                if (lo > 0x8000000000000000UL) throw new OverflowException();
                if (lo == 0x8000000000000000UL) return long.MinValue;
                return -(long)lo;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Implicitly creates a value from a 32-bit signed integer.
            /// </summary>
            /// <param name="v">
            /// The integer value.
            /// </param>
            /// <returns>
            /// The corresponding <see cref="BigBigInteger" />.
            /// </returns>
            public static implicit operator BigBigInteger(
                int v /* in */
                )
            {
                return new BigBigInteger(v);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Implicitly creates a value from a 64-bit signed integer.
            /// </summary>
            /// <param name="v">
            /// The integer value.
            /// </param>
            /// <returns>
            /// The corresponding <see cref="BigBigInteger" />.
            /// </returns>
            public static implicit operator BigBigInteger(
                long v /* in */
                )
            {
                return new BigBigInteger(v);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Implicitly creates a value from a 32-bit unsigned integer.
            /// </summary>
            /// <param name="v">
            /// The unsigned integer value.
            /// </param>
            /// <returns>
            /// The corresponding <see cref="BigBigInteger" />.
            /// </returns>
            public static implicit operator BigBigInteger(
                uint v /* in */
                )
            {
                return new BigBigInteger(v);
            }

            ///////////////////////////////////////////////////////////////////

#if HAVE_SYSTEM_NUMERICS
            /// <summary>
            /// Implicitly converts a framework
            /// <see cref="System.Numerics.BigInteger" /> to a
            /// <see cref="BigBigInteger" />.
            /// </summary>
            /// <param name="v">
            /// The framework integer value.
            /// </param>
            /// <returns>
            /// The corresponding <see cref="BigBigInteger" />.
            /// </returns>
            public static implicit operator BigBigInteger(
                System.Numerics.BigInteger v /* in */
                )
            {
                return FromBigInteger(v);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Implicitly converts a <see cref="BigBigInteger" /> to a
            /// framework <see cref="System.Numerics.BigInteger" />.
            /// </summary>
            /// <param name="v">
            /// The value to convert.
            /// </param>
            /// <returns>
            /// The equivalent <see cref="System.Numerics.BigInteger" />.
            /// </returns>
            public static implicit operator System.Numerics.BigInteger(
                BigBigInteger v /* in */
                )
            {
                return v.ToBigInteger();
            }
#endif
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region System.Object Overrides
            /// <summary>
            /// Determines whether the specified object is a
            /// <see cref="BigBigInteger" /> with the same value.
            /// </summary>
            /// <param name="obj">
            /// The object to compare with this value.
            /// </param>
            /// <returns>
            /// True if <paramref name="obj" /> is an equal
            /// <see cref="BigBigInteger" />; otherwise false.
            /// </returns>
            public override bool Equals(
                object obj /* in */
                )
            {
                return obj is BigBigInteger && this == (BigBigInteger)obj;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns a hash code for this value derived from its sign and
            /// magnitude.
            /// </summary>
            /// <returns>
            /// A hash code for this value.
            /// </returns>
            public override int GetHashCode()
            {
                int h = 17 + _sign;

                for (int i = 0; i < _limbs.Length; i++)
                    h = h * 31 + (int)_limbs[i];

                return h;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Methods
            /// <summary>
            /// Initializes this instance from a 64-bit signed integer.
            /// </summary>
            /// <param name="v">
            /// The integer value.
            /// </param>
            private void InitFromLong(
                long v /* in */
                )
            {
                if (v == 0)
                {
                    _limbs = EmptyLimbs;
                    _sign = 0;
                    return;
                }

                int sign = v < 0 ? -1 : 1;
                ulong mag = v < 0 ? ((ulong)(-(v + 1)) + 1UL) : (ulong)v;
                uint lo = (uint)mag;
                uint hi = (uint)(mag >> 32);

                _limbs = hi == 0 ? new uint[] { lo } : new uint[] { lo, hi };
                _sign = sign;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Initializes this instance from a little-endian
            /// two's-complement byte array.
            /// </summary>
            /// <param name="value">
            /// The little-endian two's-complement bytes.
            /// </param>
            private void InitFromLeTwosComplement(
                byte[] value /* in */
                )
            {
                if (value == null || value.Length == 0)
                {
                    _limbs = EmptyLimbs;
                    _sign = 0;
                    return;
                }

                bool negative = (value[value.Length - 1] & 0x80) != 0;

                if (!negative)
                {
                    uint[] m = LimbsFromLeBytes(value);

                    _limbs = m;
                    _sign = m.Length == 0 ? 0 : 1;
                    return;
                }

                byte[] mag = new byte[value.Length];
                int carry = 1;

                for (int i = 0; i < value.Length; i++)
                {
                    int x = (value[i] ^ 0xFF) + carry;

                    mag[i] = (byte)x;
                    carry = x >> 8;
                }

                uint[] mm = LimbsFromLeBytes(mag);

                _limbs = mm;
                _sign = mm.Length == 0 ? 0 : -1;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Static Methods
            //
            // NOTE: Builds a normalized value from a magnitude and a sign; an
            //       empty (zero) magnitude always collapses to the shared
            //       Zero instance.
            //
            /// <summary>
            /// Builds a normalized value from a magnitude and a sign; an
            /// empty (zero) magnitude collapses to <see cref="Zero" />.
            /// </summary>
            /// <param name="mag">
            /// The little-endian magnitude limbs.
            /// </param>
            /// <param name="sign">
            /// The sign to apply when the magnitude is non-empty.
            /// </param>
            /// <returns>
            /// The normalized value.
            /// </returns>
            private static BigBigInteger Make(
                uint[] mag, /* in */
                int sign    /* in */
                )
            {
                uint[] t = Trim(mag);

                if (t.Length == 0)
                    return Zero;

                BigBigInteger r = new BigBigInteger();

                r._limbs = t;
                r._sign = sign;

                return r;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns <paramref name="a" /> with any leading-zero
            /// (most-significant) limbs removed.
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs to trim.
            /// </param>
            /// <returns>
            /// The trimmed magnitude (the shared empty array when zero).
            /// </returns>
            private static uint[] Trim(
                uint[] a /* in */
                )
            {
                int n = a.Length;

                while (n > 0 && a[n - 1] == 0u) n--;

                if (n == a.Length) return a;
                if (n == 0) return EmptyLimbs;

                uint[] r = new uint[n];

                Array.Copy(a, r, n);
                return r;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Compares two unsigned magnitudes of the given limb lengths.
            /// </summary>
            /// <param name="a">
            /// The first magnitude.
            /// </param>
            /// <param name="an">
            /// The used limb count of <paramref name="a" />.
            /// </param>
            /// <param name="b">
            /// The second magnitude.
            /// </param>
            /// <param name="bn">
            /// The used limb count of <paramref name="b" />.
            /// </param>
            /// <returns>
            /// -1, 0, or +1 according to whether <paramref name="a" /> is
            /// less than, equal to, or greater than <paramref name="b" />.
            /// </returns>
            private static int CompareMag(
                uint[] a, /* in */
                int an,   /* in */
                uint[] b, /* in */
                int bn    /* in */
                )
            {
                while (an > 0 && a[an - 1] == 0) an--;
                while (bn > 0 && b[bn - 1] == 0) bn--;

                if (an != bn) return an < bn ? -1 : 1;

                for (int i = an - 1; i >= 0; i--)
                    if (a[i] != b[i]) return a[i] < b[i] ? -1 : 1;

                return 0;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Converts a little-endian unsigned byte array to a trimmed limb
            /// array.
            /// </summary>
            /// <param name="le">
            /// The little-endian unsigned bytes.
            /// </param>
            /// <returns>
            /// The trimmed magnitude limbs.
            /// </returns>
            private static uint[] LimbsFromLeBytes(
                byte[] le /* in */
                )
            {
                int n = (le.Length + 3) / 4;
                uint[] r = new uint[n];

                for (int i = 0; i < le.Length; i++)
                    r[i >> 2] |= (uint)le[i] << ((i & 3) * 8);

                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Converts a magnitude to its minimal little-endian unsigned
            /// byte representation.
            /// </summary>
            /// <param name="mag">
            /// The magnitude limbs.
            /// </param>
            /// <returns>
            /// The minimal little-endian unsigned bytes.
            /// </returns>
            private static byte[] MagToLeBytesMinimal(
                uint[] mag /* in */
                )
            {
                if (mag.Length == 0) return new byte[] { 0 };

                byte[] b = new byte[mag.Length * 4];

                for (int i = 0; i < mag.Length; i++)
                {
                    b[i * 4] = (byte)mag[i];
                    b[i * 4 + 1] = (byte)(mag[i] >> 8);
                    b[i * 4 + 2] = (byte)(mag[i] >> 16);
                    b[i * 4 + 3] = (byte)(mag[i] >> 24);
                }

                int len = b.Length;

                while (len > 1 && b[len - 1] == 0) len--;

                if (len == b.Length) return b;

                byte[] r = new byte[len];

                Array.Copy(b, r, len);
                return r;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns the magnitude <paramref name="a" /> shifted left by
            /// <paramref name="bits" /> bits.
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <param name="bits">
            /// The number of bits.
            /// </param>
            /// <returns>
            /// The shifted magnitude.
            /// </returns>
            private static uint[] ShiftLeftMag(
                uint[] a, /* in */
                int bits  /* in */
                )
            {
                if (a.Length == 0) return EmptyLimbs;

                int limbShift = bits >> 5;
                int bitShift = bits & 31;
                int n = a.Length;
                uint[] r = new uint[n + limbShift + 1];

                if (bitShift == 0)
                {
                    for (int i = 0; i < n; i++) r[i + limbShift] = a[i];
                }
                else
                {
                    uint carry = 0;

                    for (int i = 0; i < n; i++)
                    {
                        r[i + limbShift] = (a[i] << bitShift) | carry;
                        carry = a[i] >> (32 - bitShift);
                    }

                    r[n + limbShift] = carry;
                }

                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns the magnitude <paramref name="a" /> shifted right by
            /// <paramref name="bits" /> bits (toward zero).
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <param name="bits">
            /// The number of bits.
            /// </param>
            /// <returns>
            /// The shifted magnitude.
            /// </returns>
            private static uint[] ShiftRightMag(
                uint[] a, /* in */
                int bits  /* in */
                )
            {
                int limbShift = bits >> 5;
                int bitShift = bits & 31;

                if (limbShift >= a.Length) return EmptyLimbs;

                int n = a.Length - limbShift;
                uint[] r = new uint[n];

                if (bitShift == 0)
                {
                    for (int i = 0; i < n; i++) r[i] = a[i + limbShift];
                }
                else
                {
                    for (int i = 0; i < n; i++)
                    {
                        uint lo = a[i + limbShift] >> bitShift;
                        uint hi = (i + limbShift + 1 < a.Length)
                            ? (a[i + limbShift + 1] << (32 - bitShift))
                            : 0u;

                        r[i] = lo | hi;
                    }
                }

                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Determines whether any of the lowest <paramref name="bits" />
            /// bits of magnitude <paramref name="a" /> are set (used to round
            /// an arithmetic right shift toward negative infinity).
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <param name="bits">
            /// The number of low bits to test.
            /// </param>
            /// <returns>
            /// True if any of the low bits are set; otherwise false.
            /// </returns>
            private static bool AnyLowBitsSet(
                uint[] a, /* in */
                int bits  /* in */
                )
            {
                int limbShift = bits >> 5;
                int bitShift = bits & 31;
                int full = limbShift < a.Length ? limbShift : a.Length;

                for (int i = 0; i < full; i++)
                    if (a[i] != 0) return true;

                if (bitShift > 0 && limbShift < a.Length)
                    if ((a[limbShift] & ((1u << bitShift) - 1u)) != 0)
                        return true;

                return false;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns the sum of two unsigned magnitudes.
            /// </summary>
            /// <param name="a">
            /// The first magnitude.
            /// </param>
            /// <param name="b">
            /// The second magnitude.
            /// </param>
            /// <returns>
            /// The sum magnitude.
            /// </returns>
            private static uint[] AddMag(
                uint[] a, /* in */
                uint[] b  /* in */
                )
            {
                if (a.Length < b.Length) { uint[] t = a; a = b; b = t; }

                uint[] r = new uint[a.Length + 1];
                ulong carry = 0;
                int i = 0;

                for (; i < b.Length; i++)
                {
                    ulong s = (ulong)a[i] + b[i] + carry;

                    r[i] = (uint)s;
                    carry = s >> 32;
                }

                for (; i < a.Length; i++)
                {
                    ulong s = (ulong)a[i] + carry;

                    r[i] = (uint)s;
                    carry = s >> 32;
                }

                r[i] = (uint)carry;
                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Magnitude subtraction; the caller guarantees a >= b.
            //
            /// <summary>
            /// Returns the difference <paramref name="a" /> -
            /// <paramref name="b" /> of two unsigned magnitudes, assuming
            /// <paramref name="a" /> &gt;= <paramref name="b" />.
            /// </summary>
            /// <param name="a">
            /// The minuend magnitude.
            /// </param>
            /// <param name="b">
            /// The subtrahend magnitude.
            /// </param>
            /// <returns>
            /// The difference magnitude.
            /// </returns>
            private static uint[] SubMag(
                uint[] a, /* in */
                uint[] b  /* in */
                )
            {
                uint[] r = new uint[a.Length];
                long borrow = 0;
                int i = 0;

                for (; i < b.Length; i++)
                {
                    long d = (long)a[i] - b[i] - borrow;

                    r[i] = (uint)d;
                    borrow = (d >> 63) & 1;
                }

                for (; i < a.Length; i++)
                {
                    long d = (long)a[i] - borrow;

                    r[i] = (uint)d;
                    borrow = (d >> 63) & 1;
                }

                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Multiplies two unsigned magnitudes, using the NTT multiply at
            /// or above <see cref="NttThresholdLimbs" /> and the schoolbook
            /// multiply otherwise.
            /// </summary>
            /// <param name="a">
            /// The first magnitude.
            /// </param>
            /// <param name="b">
            /// The second magnitude.
            /// </param>
            /// <returns>
            /// The product magnitude.
            /// </returns>
            private static uint[] MulMag(
                uint[] a, /* in */
                uint[] b  /* in */
                )
            {
                if (a.Length == 0 || b.Length == 0) return EmptyLimbs;

                int min = a.Length < b.Length ? a.Length : b.Length;

                if (min >= NttThresholdLimbs)
                {
                    uint[] ntt = TryMulNtt(a, b);

                    if (ntt != null) return ntt;
                }

                return MulSchoolbook(a, b);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Multiplies two unsigned magnitudes using the O(n*m) schoolbook
            /// algorithm.
            /// </summary>
            /// <param name="a">
            /// The first magnitude.
            /// </param>
            /// <param name="b">
            /// The second magnitude.
            /// </param>
            /// <returns>
            /// The product magnitude.
            /// </returns>
            private static uint[] MulSchoolbook(
                uint[] a, /* in */
                uint[] b  /* in */
                )
            {
                uint[] r = new uint[a.Length + b.Length];

                for (int i = 0; i < a.Length; i++)
                {
                    ulong ai = a[i];
                    ulong carry = 0;

                    for (int j = 0; j < b.Length; j++)
                    {
                        ulong s = (ulong)r[i + j] + ai * b[j] + carry;

                        r[i + j] = (uint)s;
                        carry = s >> 32;
                    }

                    r[i + b.Length] = (uint)carry;
                }

                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Exact integer multiply via two-prime NTT convolution over
            //       base-2^16 digits, recombined with CRT (Garner). Both
            //       primes are < 2^31 and of the form k*2^e+1; control flow
            //       depends only on the operand SIZE, never values. Returns
            //       null if the inputs exceed the two-prime coefficient bound
            //       (caller falls back to schoolbook).
            //
            /// <summary>
            /// Attempts an exact multiply of two magnitudes via a two-prime
            /// NTT convolution recombined with the CRT.
            /// </summary>
            /// <param name="a">
            /// The first magnitude.
            /// </param>
            /// <param name="b">
            /// The second magnitude.
            /// </param>
            /// <returns>
            /// The product magnitude, or null when the inputs exceed the
            /// two-prime coefficient bound (so the caller falls back to the
            /// schoolbook multiply).
            /// </returns>
            private static uint[] TryMulNtt(
                uint[] a, /* in */
                uint[] b  /* in */
                )
            {
                int da = a.Length * 2, db = b.Length * 2;
                int need = da + db;
                int n = 1; while (n < need) n <<= 1;

                ulong maxCoeff = (ulong)n * 65535UL * 65535UL;

                if (maxCoeff >= (ulong)NttP1 * (ulong)NttP2) return null;
                if (n > (1 << 26)) return null;

                long[] fa1 = new long[n];
                long[] fb1 = new long[n];
                long[] fa2 = new long[n];
                long[] fb2 = new long[n];

                for (int i = 0; i < a.Length; i++)
                {
                    fa1[2 * i] = a[i] & 0xFFFF;
                    fa1[2 * i + 1] = (a[i] >> 16) & 0xFFFF;
                }

                for (int i = 0; i < b.Length; i++)
                {
                    fb1[2 * i] = b[i] & 0xFFFF;
                    fb1[2 * i + 1] = (b[i] >> 16) & 0xFFFF;
                }

                Array.Copy(fa1, fa2, n);
                Array.Copy(fb1, fb2, n);

                Ntt(fa1, false, NttP1, NttG1); Ntt(fb1, false, NttP1, NttG1);
                Ntt(fa2, false, NttP2, NttG2); Ntt(fb2, false, NttP2, NttG2);

                for (int i = 0; i < n; i++)
                {
                    fa1[i] = fa1[i] * fb1[i] % NttP1;
                    fa2[i] = fa2[i] * fb2[i] % NttP2;
                }

                Ntt(fa1, true, NttP1, NttG1);
                Ntt(fa2, true, NttP2, NttG2);

                long invP1modP2 = ModPowL(NttP1 % NttP2, NttP2 - 2, NttP2);
                ushort[] digits = new ushort[n + 2];
                ulong carry = 0;
                int outLen = 0;

                for (int i = 0; i < n; i++)
                {
                    long r1 = fa1[i], r2 = fa2[i];
                    long t = (((r2 - r1) % NttP2 + NttP2) % NttP2) *
                        invP1modP2 % NttP2;
                    ulong x = (ulong)r1 + (ulong)NttP1 * (ulong)t;
                    ulong cur = x + carry;

                    digits[i] = (ushort)(cur & 0xFFFF);
                    carry = cur >> 16;
                    outLen = i + 1;
                }

                for (int i = n; carry != 0; i++)
                {
                    digits[i] = (ushort)(carry & 0xFFFF);
                    carry >>= 16;
                    outLen = i + 1;
                }

                int limbs = (outLen + 1) / 2;
                uint[] r = new uint[limbs];

                for (int i = 0; i < outLen; i++)
                    r[i >> 1] |= (uint)digits[i] << ((i & 1) * 16);

                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Iterative in-place NTT; arr length must be a power of two
            //       and its values in [0, mod).
            //
            /// <summary>
            /// Performs an in-place number-theoretic transform of
            /// <paramref name="arr" /> (or its inverse when
            /// <paramref name="invert" /> is true) modulo
            /// <paramref name="mod" /> using the given
            /// <paramref name="root" />; the length must be a power of two
            /// and the values must be in [0, mod).
            /// </summary>
            /// <param name="arr">
            /// The transform buffer, modified in place.
            /// </param>
            /// <param name="invert">
            /// True to perform the inverse transform.
            /// </param>
            /// <param name="mod">
            /// The NTT prime modulus.
            /// </param>
            /// <param name="root">
            /// The transform root (a primitive root of
            /// <paramref name="mod" />).
            /// </param>
            private static void Ntt(
                long[] arr,  /* in, out */
                bool invert, /* in */
                long mod,    /* in */
                long root    /* in */
                )
            {
                int n = arr.Length;

                for (int i = 1, j = 0; i < n; i++)
                {
                    int bit = n >> 1;

                    for (; (j & bit) != 0; bit >>= 1) j ^= bit;

                    j ^= bit;

                    if (i < j)
                    {
                        long tmp = arr[i];
                        arr[i] = arr[j];
                        arr[j] = tmp;
                    }
                }

                for (int len = 2; len <= n; len <<= 1)
                {
                    long w = invert
                        ? ModPowL(root, mod - 1 - (mod - 1) / len, mod)
                        : ModPowL(root, (mod - 1) / len, mod);
                    int half = len >> 1;

                    for (int i = 0; i < n; i += len)
                    {
                        long wn = 1;

                        for (int k = 0; k < half; k++)
                        {
                            long u = arr[i + k];
                            long v = arr[i + k + half] * wn % mod;

                            arr[i + k] = (u + v) % mod;
                            arr[i + k + half] = (u - v + mod) % mod;
                            wn = wn * w % mod;
                        }
                    }
                }

                if (invert)
                {
                    long ninv = ModPowL(n, mod - 2, mod);

                    for (int i = 0; i < n; i++) arr[i] = arr[i] * ninv % mod;
                }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Modular exponentiation for small (&lt; 2^31) moduli, used
            /// internally by the NTT recombination.
            /// </summary>
            /// <param name="b">
            /// The base value.
            /// </param>
            /// <param name="e">
            /// The exponent.
            /// </param>
            /// <param name="mod">
            /// The modulus.
            /// </param>
            /// <returns>
            /// <paramref name="b" /> raised to <paramref name="e" /> modulo
            /// <paramref name="mod" />.
            /// </returns>
            private static long ModPowL(
                long b,  /* in */
                long e,  /* in */
                long mod /* in */
                )
            {
                long r = 1; b %= mod; if (b < 0) b += mod;

                while (e > 0)
                {
                    if ((e & 1) == 1) r = r * b % mod;
                    b = b * b % mod;
                    e >>= 1;
                }

                return r;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Knuth Algorithm D unsigned division; returns quotient and
            //       remainder magnitudes.
            //
            /// <summary>
            /// Computes the quotient and remainder of two unsigned magnitudes
            /// using Knuth's Algorithm D.
            /// </summary>
            /// <param name="u">
            /// The dividend magnitude.
            /// </param>
            /// <param name="v">
            /// The divisor magnitude.
            /// </param>
            /// <param name="q">
            /// Receives the quotient magnitude.
            /// </param>
            /// <param name="r">
            /// Receives the remainder magnitude.
            /// </param>
            private static void DivRemMag(
                uint[] u,     /* in */
                uint[] v,     /* in */
                out uint[] q, /* out */
                out uint[] r  /* out */
                )
            {
                int n = v.Length, m = u.Length - v.Length;

                if (v.Length == 0) throw new DivideByZeroException();

                if (CompareMag(u, u.Length, v, v.Length) < 0)
                {
                    q = EmptyLimbs;
                    r = Trim((uint[])u.Clone());

                    return;
                }

                if (n == 1)
                {
                    uint d = v[0];
                    uint[] qq = new uint[u.Length];
                    ulong rem = 0;

                    for (int i = u.Length - 1; i >= 0; i--)
                    {
                        ulong cur = (rem << 32) | u[i];

                        qq[i] = (uint)(cur / d);
                        rem = cur % d;
                    }

                    q = Trim(qq);
                    r = rem == 0 ? EmptyLimbs : new uint[] { (uint)rem };
                    return;
                }

                int shift = 0;
                uint top = v[n - 1];

                while ((top & 0x80000000u) == 0) { top <<= 1; shift++; }

                uint[] vn = ShlBitsExact(v, shift, n);
                uint[] un = ShlBits(u, shift, u.Length + 1);
                uint[] qq2 = new uint[m + 1];
                ulong b = 1UL << 32;

                for (int j = m; j >= 0; j--)
                {
                    ulong num = ((ulong)un[j + n] << 32) | un[j + n - 1];
                    ulong qhat = num / vn[n - 1];
                    ulong rhat = num % vn[n - 1];

                    while (qhat >= b ||
                        qhat * vn[n - 2] > ((rhat << 32) | un[j + n - 2]))
                    {
                        qhat--; rhat += vn[n - 1];
                        if (rhat >= b) break;
                    }

                    long borrow = 0;

                    for (int i = 0; i < n; i++)
                    {
                        ulong p = qhat * vn[i];
                        long sub = (long)un[i + j] - borrow - (long)(uint)p;

                        un[i + j] = (uint)sub;
                        borrow = (long)(p >> 32) - (sub >> 32);
                    }

                    long subt = (long)un[j + n] - borrow;

                    un[j + n] = (uint)subt;

                    if (subt < 0)
                    {
                        qhat--;
                        ulong c2 = 0;

                        for (int i = 0; i < n; i++)
                        {
                            ulong s = (ulong)un[i + j] + vn[i] + c2;

                            un[i + j] = (uint)s;
                            c2 = s >> 32;
                        }

                        un[j + n] = (uint)(un[j + n] + c2);
                    }

                    qq2[j] = (uint)qhat;
                }

                q = Trim(qq2);
                r = ShrBits(un, shift, n);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns <paramref name="a" /> shifted left by
            /// <paramref name="shift" /> bits into an array of exactly
            /// <paramref name="outLen" /> limbs.
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <param name="shift">
            /// The number of bits.
            /// </param>
            /// <param name="outLen">
            /// The exact output limb count.
            /// </param>
            /// <returns>
            /// The shifted magnitude in an <paramref name="outLen" />-limb
            /// array.
            /// </returns>
            private static uint[] ShlBitsExact(
                uint[] a,  /* in */
                int shift, /* in */
                int outLen /* in */
                )
            {
                uint[] r = new uint[outLen];

                if (shift == 0)
                {
                    Array.Copy(a, r, Math.Min(a.Length, outLen));
                    return r;
                }

                uint carry = 0;

                for (int i = 0; i < a.Length && i < outLen; i++)
                {
                    r[i] = (a[i] << shift) | carry;
                    carry = a[i] >> (32 - shift);
                }

                return r;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns <paramref name="a" /> shifted left by
            /// <paramref name="shift" /> bits, truncated or padded to
            /// <paramref name="outLen" /> limbs.
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <param name="shift">
            /// The number of bits.
            /// </param>
            /// <param name="outLen">
            /// The output limb count.
            /// </param>
            /// <returns>
            /// The shifted magnitude in an <paramref name="outLen" />-limb
            /// array.
            /// </returns>
            private static uint[] ShlBits(
                uint[] a,  /* in */
                int shift, /* in */
                int outLen /* in */
                )
            {
                uint[] r = new uint[outLen];

                if (shift == 0)
                {
                    Array.Copy(a, r, Math.Min(a.Length, outLen));
                    return r;
                }

                uint carry = 0;
                int i = 0;

                for (; i < a.Length; i++)
                {
                    r[i] = (a[i] << shift) | carry;
                    carry = a[i] >> (32 - shift);
                }

                if (i < outLen) r[i] = carry;

                return r;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns <paramref name="a" /> shifted right by
            /// <paramref name="shift" /> bits into an array of
            /// <paramref name="outLen" /> limbs.
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <param name="shift">
            /// The number of bits.
            /// </param>
            /// <param name="outLen">
            /// The output limb count.
            /// </param>
            /// <returns>
            /// The shifted magnitude in an <paramref name="outLen" />-limb
            /// array.
            /// </returns>
            private static uint[] ShrBits(
                uint[] a,  /* in */
                int shift, /* in */
                int outLen /* in */
                )
            {
                uint[] r = new uint[outLen];

                if (shift == 0)
                {
                    Array.Copy(a, r, Math.Min(a.Length, outLen));
                    return Trim(r);
                }

                for (int i = 0; i < outLen; i++)
                {
                    uint lo = a[i] >> shift;

                    uint hi = (i + 1 < a.Length) ?
                        (a[i + 1] << (32 - shift)) : 0u;

                    r[i] = lo | hi;
                }

                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Computes the Barrett constant mu = floor(2^(64n) /
            /// <paramref name="m" />), one big division per modulus.
            /// </summary>
            /// <param name="m">
            /// The modulus magnitude.
            /// </param>
            /// <param name="n">
            /// The modulus limb count.
            /// </param>
            /// <returns>
            /// The Barrett constant mu.
            /// </returns>
            private static uint[] ComputeBarrettMu(
                uint[] m, /* in */
                int n     /* in */
                )
            {
                uint[] num = new uint[2 * n + 1];

                num[2 * n] = 1u;

                uint[] q, r;

                DivRemMag(num, m, out q, out r);
                return q;
            }

            ///////////////////////////////////////////////////////////////////

            //
            // NOTE: Barrett reduction (HAC 14.42, base b=2^32, k=n): returns
            //       x mod m in [0, m) for x < m^2. The two products use the
            //       NTT multiply; the final corrections (<= 2) are
            //       constant-time mask-selects.
            //
            /// <summary>
            /// Reduces <paramref name="x" /> modulo <paramref name="m" />
            /// using the precomputed Barrett constant <paramref name="mu" />
            /// (HAC 14.42), returning a value in [0, m) for x &lt; m^2.
            /// </summary>
            /// <param name="x">
            /// The value to reduce.
            /// </param>
            /// <param name="m">
            /// The modulus magnitude.
            /// </param>
            /// <param name="mu">
            /// The precomputed Barrett constant.
            /// </param>
            /// <param name="n">
            /// The modulus limb count.
            /// </param>
            /// <returns>
            /// <paramref name="x" /> modulo <paramref name="m" />.
            /// </returns>
            private static uint[] BarrettReduce(
                uint[] x,  /* in */
                uint[] m,  /* in */
                uint[] mu, /* in */
                int n      /* in */
                )
            {
                if (CompareMag(x, x.Length, m, n) < 0)
                    return Trim((uint[])x.Clone());

                uint[] q1 = ShrLimbs(x, n - 1);
                uint[] q2 = MulMag(q1, mu);
                uint[] q3 = ShrLimbs(q2, n + 1);
                uint[] r1 = TruncLimbs(x, n + 1);
                uint[] r2 = TruncLimbs(MulMag(q3, m), n + 1);
                uint[] r;

                if (CompareMag(r1, r1.Length, r2, r2.Length) >= 0)
                {
                    r = SubMag(r1, r2);
                }
                else
                {
                    uint[] big = new uint[n + 2]; big[n + 1] = 1u;

                    r = SubMag(AddMag(big, r1), r2);
                }

                r = CondSubFixed(r, m, n);
                r = CondSubFixed(r, m, n);

                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Constant-time conditional subtract over (n+1) limbs: returns
            /// <paramref name="r" /> - <paramref name="m" /> when
            /// <paramref name="r" /> &gt;= <paramref name="m" />, otherwise
            /// <paramref name="r" />.
            /// </summary>
            /// <param name="r">
            /// The value, n+1 limbs.
            /// </param>
            /// <param name="m">
            /// The modulus magnitude.
            /// </param>
            /// <param name="n">
            /// The modulus limb count.
            /// </param>
            /// <returns>
            /// The conditionally reduced value.
            /// </returns>
            private static uint[] CondSubFixed(
                uint[] r, /* in */
                uint[] m, /* in */
                int n     /* in */
                )
            {
                int w = n + 1;
                uint[] rp = Pad(r, w);
                uint[] diff = new uint[w];
                long borrow = 0;

                for (int j = 0; j < w; j++)
                {
                    long mj = j < m.Length ? m[j] : 0;
                    long d = (long)rp[j] - mj - borrow;

                    diff[j] = (uint)d;
                    borrow = (d >> 63) & 1;
                }

                uint mask = (uint)(borrow - 1);
                uint[] outr = new uint[w];

                for (int j = 0; j < w; j++)
                    outr[j] = (rp[j] & ~mask) | (diff[j] & mask);

                return outr;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Constant-time selection of <paramref name="table" />[<paramref
            /// name="d" />] for variable-length entries, without indexing
            /// memory by <paramref name="d" />.
            /// </summary>
            /// <param name="table">
            /// The table of candidate entries.
            /// </param>
            /// <param name="d">
            /// The secret index to select.
            /// </param>
            /// <param name="tableSize">
            /// The number of entries in <paramref name="table" />.
            /// </param>
            /// <returns>
            /// A copy of the selected entry.
            /// </returns>
            private static uint[] CtSelectVar(
                uint[][] table, /* in */
                int d,          /* in */
                int tableSize   /* in */
                )
            {
                int max = 0;

                for (int i = 0; i < tableSize; i++)
                    if (table[i].Length > max)
                        max = table[i].Length;

                uint[] sel = new uint[max == 0 ? 1 : max];

                for (int idx = 0; idx < tableSize; idx++)
                {
                    uint eq = CtEqMask(idx, d);
                    uint[] e = table[idx];

                    for (int j = 0; j < e.Length; j++)
                        sel[j] |= e[j] & eq;
                }

                return sel;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns <paramref name="a" /> with its <paramref name="k" />
            /// least-significant limbs removed (division by 2^(32k)).
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <param name="k">
            /// The number of low limbs to drop.
            /// </param>
            /// <returns>
            /// The high portion of the magnitude.
            /// </returns>
            private static uint[] ShrLimbs(
                uint[] a, /* in */
                int k     /* in */
                )
            {
                if (k <= 0) return (uint[])a.Clone();
                if (k >= a.Length) return EmptyLimbs;

                uint[] r = new uint[a.Length - k];

                Array.Copy(a, k, r, 0, r.Length);
                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns the low <paramref name="k" /> limbs of
            /// <paramref name="a" />.
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <param name="k">
            /// The number of low limbs to keep.
            /// </param>
            /// <returns>
            /// The low portion of the magnitude.
            /// </returns>
            private static uint[] TruncLimbs(
                uint[] a, /* in */
                int k     /* in */
                )
            {
                if (k >= a.Length) return Trim((uint[])a.Clone());

                uint[] r = new uint[k];

                Array.Copy(a, 0, r, 0, k);
                return Trim(r);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Computes the Montgomery n' value (-m0^-1 mod 2^32) from the
            /// least-significant modulus limb.
            /// </summary>
            /// <param name="m0">
            /// The least-significant modulus limb.
            /// </param>
            /// <returns>
            /// The Montgomery n' value.
            /// </returns>
            private static uint ComputeNPrime(
                uint m0 /* in */
                )
            {
                uint inv = m0;

                for (int k = 0; k < 5; k++) inv = inv * (2u - m0 * inv);

                return (uint)(0u - inv);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Computes the Montgomery R^2 mod m constant for an n-limb
            /// modulus.
            /// </summary>
            /// <param name="m">
            /// The modulus magnitude.
            /// </param>
            /// <param name="n">
            /// The modulus limb count.
            /// </param>
            /// <returns>
            /// The Montgomery R^2 mod m constant.
            /// </returns>
            private static uint[] ComputeRR(
                uint[] m, /* in */
                int n     /* in */
                )
            {
                uint[] t = new uint[n]; t[0] = 1u;
                int total = 64 * n;

                for (int b = 0; b < total; b++)
                {
                    uint carry = 0;

                    for (int j = 0; j < n; j++)
                    {
                        uint nv = (t[j] << 1) | carry;
                        carry = t[j] >> 31;
                        t[j] = nv;
                    }

                    bool ge = carry != 0 || CompareMag(t, n, m, n) >= 0;

                    if (ge) SubInPlace(t, m, n);
                }

                return t;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Subtracts magnitude <paramref name="m" /> from
            /// <paramref name="t" /> in place over <paramref name="n" />
            /// limbs.
            /// </summary>
            /// <param name="t">
            /// The value to reduce, modified in place.
            /// </param>
            /// <param name="m">
            /// The modulus magnitude.
            /// </param>
            /// <param name="n">
            /// The modulus limb count.
            /// </param>
            private static void SubInPlace(
                uint[] t, /* in, out */
                uint[] m, /* in */
                int n     /* in */
                )
            {
                long borrow = 0;

                for (int j = 0; j < n; j++)
                {
                    long d = (long)t[j] - m[j] - borrow;

                    t[j] = (uint)d;
                    borrow = (d >> 63) & 1;
                }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// CIOS Montgomery multiplication of two n-limb values modulo
            /// <paramref name="m" /> with n' = <paramref name="mp" />.
            /// </summary>
            /// <param name="a">
            /// The first factor (n limbs, Montgomery form).
            /// </param>
            /// <param name="b">
            /// The second factor (n limbs, Montgomery form).
            /// </param>
            /// <param name="m">
            /// The modulus magnitude.
            /// </param>
            /// <param name="mp">
            /// The Montgomery n' value.
            /// </param>
            /// <param name="n">
            /// The modulus limb count.
            /// </param>
            /// <returns>
            /// The n-limb Montgomery product.
            /// </returns>
            private static uint[] MontMul(
                uint[] a, /* in */
                uint[] b, /* in */
                uint[] m, /* in */
                uint mp,  /* in */
                int n     /* in */
                )
            {
                uint[] t = new uint[n + 2];

                for (int i = 0; i < n; i++)
                {
                    ulong bi = b[i];
                    ulong c = 0;

                    for (int j = 0; j < n; j++)
                    {
                        ulong s = (ulong)t[j] + (ulong)a[j] * bi + c;

                        t[j] = (uint)s;
                        c = s >> 32;
                    }

                    {
                        ulong s = (ulong)t[n] + c;

                        t[n] = (uint)s;
                        t[n + 1] = (uint)(s >> 32);
                    }

                    uint mc = (uint)((ulong)t[0] * mp);

                    {
                        ulong s = (ulong)t[0] + (ulong)mc * m[0];

                        c = s >> 32;
                    }

                    for (int j = 1; j < n; j++)
                    {
                        ulong s = (ulong)t[j] + (ulong)mc * m[j] + c;

                        t[j - 1] = (uint)s;
                        c = s >> 32;
                    }

                    {
                        ulong s = (ulong)t[n] + c;

                        t[n - 1] = (uint)s;
                        t[n] = t[n + 1] + (uint)(s >> 32);
                    }
                }

                uint[] result = CondSub(t, m, n);

                Array.Clear(t, 0, t.Length);
                return result;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Constant-time conditional subtract where <paramref name="t" />
            /// has n+1 words.
            /// </summary>
            /// <param name="t">
            /// The value, n+1 words.
            /// </param>
            /// <param name="m">
            /// The modulus magnitude.
            /// </param>
            /// <param name="n">
            /// The modulus limb count.
            /// </param>
            /// <returns>
            /// The n-word conditionally reduced result.
            /// </returns>
            private static uint[] CondSub(
                uint[] t, /* in */
                uint[] m, /* in */
                int n     /* in */
                )
            {
                uint[] diff = new uint[n];
                long borrow = 0;

                for (int j = 0; j < n; j++)
                {
                    long d = (long)t[j] - m[j] - borrow;

                    diff[j] = (uint)d;
                    borrow = (d >> 63) & 1;
                }

                long dn = (long)t[n] - borrow;

                borrow = (dn >> 63) & 1;

                uint mask = (uint)(borrow - 1);
                uint[] r = new uint[n];

                for (int j = 0; j < n; j++)
                    r[j] = (t[j] & ~mask) | (diff[j] & mask);

                return r;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Constant-time selection of <paramref name="table" />[<paramref
            /// name="d" />] (n limbs) without indexing memory by
            /// <paramref name="d" />.
            /// </summary>
            /// <param name="table">
            /// The table of candidate entries.
            /// </param>
            /// <param name="d">
            /// The secret index to select.
            /// </param>
            /// <param name="n">
            /// The limb count of each entry.
            /// </param>
            /// <param name="tableSize">
            /// The number of entries in <paramref name="table" />.
            /// </param>
            /// <returns>
            /// A copy of the selected entry.
            /// </returns>
            private static uint[] CtSelect(
                uint[][] table, /* in */
                int d,          /* in */
                int n,          /* in */
                int tableSize   /* in */
                )
            {
                uint[] sel = new uint[n];

                for (int idx = 0; idx < tableSize; idx++)
                {
                    uint eq = CtEqMask(idx, d);
                    uint[] e = table[idx];

                    for (int j = 0; j < n; j++) sel[j] |= e[j] & eq;
                }

                return sel;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns an all-ones mask when <paramref name="a" /> equals
            /// <paramref name="b" /> and zero otherwise, in constant time.
            /// </summary>
            /// <param name="a">
            /// The first value.
            /// </param>
            /// <param name="b">
            /// The second value.
            /// </param>
            /// <returns>
            /// 0xFFFFFFFF if the values are equal; otherwise 0.
            /// </returns>
            private static uint CtEqMask(
                int a, /* in */
                int b  /* in */
                )
            {
                uint x = (uint)(a ^ b);
                uint nz = (uint)((x | (uint)(-(int)x)) >> 31);

                return nz - 1u;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns <paramref name="a" /> zero-extended to exactly
            /// <paramref name="n" /> limbs.
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <param name="n">
            /// The target limb count.
            /// </param>
            /// <returns>
            /// The zero-extended magnitude.
            /// </returns>
            private static uint[] Pad(
                uint[] a, /* in */
                int n     /* in */
                )
            {
                uint[] r = new uint[n];

                Array.Copy(a, r, Math.Min(a.Length, n));
                return r;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns <paramref name="a" /> modulo <paramref name="m" />
            /// (magnitudes).
            /// </summary>
            /// <param name="a">
            /// The dividend magnitude.
            /// </param>
            /// <param name="m">
            /// The modulus magnitude.
            /// </param>
            /// <returns>
            /// The remainder magnitude.
            /// </returns>
            private static uint[] Mod(
                uint[] a, /* in */
                uint[] m  /* in */
                )
            {
                uint[] q, r;

                DivRemMag(a, m, out q, out r);
                return r;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns the number of significant bits in magnitude
            /// <paramref name="a" />.
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <returns>
            /// The bit length of <paramref name="a" />.
            /// </returns>
            private static int BitLengthLimbs(
                uint[] a /* in */
                )
            {
                int n = a.Length;

                while (n > 0 && a[n - 1] == 0) n--;

                if (n == 0) return 0;

                uint top = a[n - 1];
                int b = 0;

                while (top != 0) { top >>= 1; b++; }

                return (n - 1) * 32 + b;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Extracts <paramref name="count" /> bits from magnitude
            /// <paramref name="a" /> starting at bit
            /// <paramref name="start" /> (used by the fixed-window exponent
            /// schedule).
            /// </summary>
            /// <param name="a">
            /// The magnitude limbs.
            /// </param>
            /// <param name="start">
            /// The starting bit position.
            /// </param>
            /// <param name="count">
            /// The number of bits to extract.
            /// </param>
            /// <returns>
            /// The extracted bits as an integer.
            /// </returns>
            private static int GetBits(
                uint[] a,  /* in */
                int start, /* in */
                int count  /* in */
                )
            {
                int result = 0;

                for (int i = 0; i < count; i++)
                {
                    int bit = start + i;
                    int limb = bit >> 5, off = bit & 31;
                    int v = (limb < a.Length)
                        ? (int)((a[limb] >> off) & 1u) : 0;

                    result |= v << i;
                }

                return result;
            }
            #endregion
        }
        #endregion
    }

    ///////////////////////////////////////////////////////////////////////////

    #region Shims for pre-.NET Framework 4.6
    //
    // NOTE: Minimal stand-ins for the .NET 4.6+ crypto types, defined
    //       only on frameworks that lack them (.NET 2.0 - 4.5). They
    //       expose exactly the members this provider and its callers use;
    //       equality is reference equality against the singletons (callers
    //       always pass the singletons).  On .NET 4.6+ / .NET Standard the
    //       real System.Security.Cryptography types are used instead and
    //       this whole region is absent.
    //
    #region HashAlgorithmName Shim Class
#if !HAVE_RSA_PADDING_API
    /// <summary>
    /// Minimal stand-in for the .NET Framework 4.6+ HashAlgorithmName
    /// type, compiled only on frameworks that lack it (.NET 2.0 - 4.5).
    /// It carries a hash-algorithm name and compares by reference against
    /// the singleton instances exposed as static fields.
    /// </summary>
    [ObjectId("8d2b1f4a-6c50-4a9e-b3d7-1e2f3a4b5c60")]
    internal struct HashAlgorithmName
    {
        #region Public Static Data
        /// <summary>
        /// The singleton naming the SHA-1 hash algorithm.
        /// </summary>
        public static readonly HashAlgorithmName SHA1 =
            new HashAlgorithmName("SHA1");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The singleton naming the SHA-256 hash algorithm.
        /// </summary>
        public static readonly HashAlgorithmName SHA256 =
            new HashAlgorithmName("SHA256");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The singleton naming the SHA-384 hash algorithm.
        /// </summary>
        public static readonly HashAlgorithmName SHA384 =
            new HashAlgorithmName("SHA384");

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The singleton naming the SHA-512 hash algorithm.
        /// </summary>
        public static readonly HashAlgorithmName SHA512 =
            new HashAlgorithmName("SHA512");
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Constructors
        /// <summary>
        /// Creates a named hash-algorithm instance. This mirrors the public
        /// constructor of the real .NET 4.6+ HashAlgorithmName so callers can
        /// build one from a dynamic algorithm name.
        /// </summary>
        /// <param name="name">
        /// The hash-algorithm name (e.g. "SHA256").
        /// </param>
        public HashAlgorithmName(
            string name /* in */
            )
        {
            this.name = name;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Properties
        /// <summary>
        /// The hash-algorithm name backing the <see cref="Name" />
        /// property.
        /// </summary>
        private readonly string name;
        /// <summary>
        /// Gets the hash-algorithm name carried by this instance.
        /// </summary>
        /// <returns>
        /// The hash-algorithm name.
        /// </returns>
        public string Name
        {
            get { return name; }
        }
        #endregion
    }
#endif
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region RSAEncryptionPadding Shim Class
#if !HAVE_RSA_PADDING_API
    /// <summary>
    /// Minimal stand-in for the .NET Framework 4.6+ RSAEncryptionPadding
    /// type, compiled only on frameworks that lack it (.NET 2.0 - 4.5).
    /// Each static field is a singleton selecting one encryption padding
    /// mode; instances compare by reference.
    /// </summary>
    [ObjectId("a4c7e9b1-2d63-4f80-95a1-b6c7d8e9f012")]
    internal sealed class RSAEncryptionPadding
    {
        #region Public Static Data
        /// <summary>
        /// The singleton selecting PKCS#1 v1.5 encryption padding.
        /// </summary>
        public static readonly RSAEncryptionPadding Pkcs1 =
            new RSAEncryptionPadding();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The singleton selecting OAEP encryption padding with SHA-1.
        /// </summary>
        public static readonly RSAEncryptionPadding OaepSHA1 =
            new RSAEncryptionPadding();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The singleton selecting OAEP encryption padding with SHA-256.
        /// </summary>
        public static readonly RSAEncryptionPadding OaepSHA256 =
            new RSAEncryptionPadding();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The singleton selecting OAEP encryption padding with SHA-384.
        /// </summary>
        public static readonly RSAEncryptionPadding OaepSHA384 =
            new RSAEncryptionPadding();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The singleton selecting OAEP encryption padding with SHA-512.
        /// </summary>
        public static readonly RSAEncryptionPadding OaepSHA512 =
            new RSAEncryptionPadding();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Creates a singleton instance; the only instances are the
        /// static fields above.
        /// </summary>
        private RSAEncryptionPadding()
        {
            // singleton-only; instances are the static fields above
        }
        #endregion
    }
#endif
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region RSASignaturePadding Shim Class
#if !HAVE_RSA_PADDING_API
    /// <summary>
    /// Minimal stand-in for the .NET Framework 4.6+ RSASignaturePadding
    /// type, compiled only on frameworks that lack it (.NET 2.0 - 4.5).
    /// Each static field is a singleton selecting one signature padding
    /// mode; instances compare by reference.
    /// </summary>
    [ObjectId("c0e1f2a3-4b56-4c78-9d0e-1f2a3b4c5d68")]
    internal sealed class RSASignaturePadding
    {
        #region Public Static Data
        /// <summary>
        /// The singleton selecting PKCS#1 v1.5 signature padding.
        /// </summary>
        public static readonly RSASignaturePadding Pkcs1 =
            new RSASignaturePadding();

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// The singleton selecting PSS signature padding.
        /// </summary>
        public static readonly RSASignaturePadding Pss =
            new RSASignaturePadding();
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constructors
        /// <summary>
        /// Creates a singleton instance; the only instances are the
        /// static fields above.
        /// </summary>
        private RSASignaturePadding()
        {
            // singleton-only; instances are the static fields above
        }
        #endregion
    }
#endif
    #endregion
    #endregion
}
