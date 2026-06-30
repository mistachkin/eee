/*
 * BaileyBorweinPlouffe.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Numerics;
using Eagle._Components.Public;

/// <summary>
/// Implements the Bailey-Borwein-Plouffe (BBP) algorithm for computing
/// arbitrary hexadecimal digits of the fractional part of pi without
/// computing the preceding digits.  It uses fixed-point arithmetic with
/// guard digits and a carry tracker for correct rounding, and periodically
/// polls the supplied interpreter so a long computation can be cancelled.
/// This backs the <c>zeus pi</c> command.
/// </summary>
internal static class BaileyBorweinPlouffe
{
    #region Private Constants
    //
    // NOTE: Default number of extra hexadecimal "guard" digits computed
    //       beyond what the caller requested.  These absorb rounding error
    //       from (a) truncating the BBP tail at a finite term count and
    //       (b) per-term round-to-nearest accumulation in the carry
    //       tracker.  Eight digits give substantial headroom for the
    //       counts encountered in practice.
    //
    /// <summary>
    /// The default number of extra hexadecimal guard digits computed beyond
    /// the requested count to absorb rounding error.
    /// </summary>
    private const int DefaultGuardDigits = 8;

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: Number of tail terms beyond "s" that are accumulated for the
    //       k > N portion of the BBP series.  After "s" tail terms, the
    //       per-term magnitude is below one scale-unit; the "+ 20" pad
    //       absorbs round-to-nearest error accumulated across the tail.
    //
    /// <summary>
    /// The number of extra tail terms accumulated for the high-index portion
    /// of the BBP series to absorb accumulated rounding error.
    /// </summary>
    private const int TailTermPadding = 20;

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: Number of inner steps between cancellation / readiness checks.
    //       Bounds the worst-case latency between a cancellation request
    //       and the resulting exception throw.
    //
    // HACK: This is purposely not read-only.
    //
    /// <summary>
    /// The number of inner steps performed between successive cancellation
    /// and readiness checks, bounding the latency of a cancellation request.
    /// </summary>
    private static long ReadyStepLimit = 100000;
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Private Static Data
    //
    // NOTE: Cached BigInteger value of sixteen, used as the base of
    //       repeated modular exponentiation in the main loop.  Caching
    //       avoids a small allocation per inner iteration.
    //
    /// <summary>
    /// The cached big-integer value sixteen, used as the base of the repeated
    /// modular exponentiation in the main loop.
    /// </summary>
    private static readonly BigInteger Sixteen = new BigInteger(16);

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: The "j" denominator offsets and "c" numerator coefficients of
    //       the four terms in the Bailey-Borwein-Plouffe formula:
    //
    //         pi = sum_{k>=0} (1 / 16^k)
    //                * (  4 / (8k + 1)
    //                   - 2 / (8k + 4)
    //                   - 1 / (8k + 5)
    //                   - 1 / (8k + 6) ).
    //
    /// <summary>
    /// The denominator offsets of the four terms of the BBP formula (the "j"
    /// values in 8k + j).
    /// </summary>
    private static readonly int[] J = { 1, 4, 5, 6 };

    /// <summary>
    /// The numerator coefficients of the four terms of the BBP formula (the
    /// "c" values).
    /// </summary>
    private static readonly int[] C = { +4, -2, -1, -1 };
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Public Methods
    //
    // NOTE: Returns the hexadecimal expansion of the fractional part of
    //       pi, starting at one-based position "startIndex" and continuing
    //       for "count" digits.  The optional "guardDigits" parameter
    //       controls how many extra hex digits of precision are computed
    //       internally to absorb rounding error; the default (eight) is
    //       suitable for counts encountered in practice.  The supplied
    //       "interpreter" (if any) is polled periodically so the work can
    //       be cancelled.
    //
    /// <summary>
    /// Computes the hexadecimal expansion of the fractional part of pi,
    /// starting at the one-based position <paramref name="startIndex" /> and
    /// continuing for <paramref name="count" /> digits.  Extra internal guard
    /// digits absorb rounding error, and the supplied interpreter, if any, is
    /// polled periodically so the computation can be cancelled.
    /// </summary>
    /// <param name="interpreter">
    /// The interpreter polled for cancellation, or null for none.
    /// </param>
    /// <param name="startIndex">
    /// The one-based position of the first hexadecimal digit to return; must
    /// be at least one.
    /// </param>
    /// <param name="count">
    /// The number of hexadecimal digits to return; must be at least one.
    /// </param>
    /// <param name="guardDigits">
    /// The number of extra guard digits to compute internally, or null to use
    /// the default.
    /// </param>
    /// <returns>
    /// The requested hexadecimal digits of pi, left-padded with zeros to the
    /// requested count.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="startIndex" /> or
    /// <paramref name="count" /> is less than one, or when a non-null
    /// <paramref name="guardDigits" /> is negative.
    /// </exception>
    public static string GetDigits(
        Interpreter interpreter, /* in: OPTIONAL */
        long startIndex,         /* in */
        int count,               /* in */
        int? guardDigits         /* in: OPTIONAL */
        )
    {
        if (startIndex < 1)
        {
            throw new ArgumentOutOfRangeException(
                "startIndex", "startIndex must be >= 1.");
        }

        if (count < 1)
        {
            throw new ArgumentOutOfRangeException(
                "count", "count must be >= 1.");
        }

        int localGuardDigits = (guardDigits != null) ?
            (int)guardDigits : DefaultGuardDigits;

        if (localGuardDigits < 0)
        {
            throw new ArgumentOutOfRangeException(
                "guardDigits", "guardDigits must be >= 0.");
        }

        //
        // NOTE: Total precision of the fixed-point sum, in hex digits.
        //       The "checked" guards against arithmetic overflow for
        //       extreme inputs.
        //
        int s = checked(count + localGuardDigits);

        //
        // NOTE: Last summation index "N" in the shifted formulation.  We
        //       extract hex digits at positions [startIndex, startIndex +
        //       count - 1]; setting N = (startIndex - 1) + s shifts those
        //       digits into the low s hex digits of the scaled integer
        //       sum (modulo "scale").
        //
        long N = checked((startIndex - 1) + (long)s);

        //
        // NOTE: Fixed-point unit (one ULP corresponds to 16^-s of the
        //       true fraction).  The final scaled sum is taken modulo
        //       "scale" to extract the desired hex window.
        //
        BigInteger scale = BigInteger.One << (4 * s);

        BigInteger sumModScale = BigInteger.Zero;

        //
        // NOTE: Each term's contribution to "sumModScale" is rounded to
        //       the nearest scale-unit.  The carry tracker keeps the
        //       discarded fractional parts at KSmall units per scale unit
        //       so the final answer is correctly rounded despite many
        //       terms.  "KSmall" is chosen large enough that the carry's
        //       integer-quotient (added back at the end) is accurate to
        //       within one scale-unit even for the maximum possible
        //       per-term residue.
        //
        long termsUpToN = 4L * (N + 1);
        int tailTerms = s + TailTermPadding;

        BigInteger approxTerms = new BigInteger(
            termsUpToN + 4L * tailTerms);

        BigInteger KSmall = NextPowerOfTwo((approxTerms * 2) + 2);
        BigInteger carryScaled = BigInteger.Zero;

        long readyLimit = ReadyStepLimit;
        long readySteps = 0;

        //
        // NOTE: Main BBP loop.  For each k in [0, N] and each (j, c), the
        //       term contributes Q mod scale (the "q" below) to
        //       "sumModScale" plus a residue (the "r" below) to the carry
        //       tracker, where:
        //
        //         Q     = floor(c * 16^e / D)
        //         q     = Q mod scale
        //         r     = (c * 16^e) mod D
        //         D     = 8k + j
        //         e     = N - k
        //
        //       Writing (c * 16^e) = (D * scale) * A + sWide with sWide in
        //       [0, D * scale), one has q = sWide / D and r = sWide mod D.
        //
        //       When e >= s (the common case for non-trivial startIndex),
        //       the wide modular reduction simplifies:
        //
        //         sWide = (c * 16^e) mod (D * 16^s)
        //               = (c * 16^s * 16^(e-s)) mod (D * 16^s)
        //               = 16^s * ((c * 16^(e-s)) mod D)
        //
        //       so we only need a SMALL-modulus exponentiation (modulo D
        //       instead of D * scale), which is dramatically cheaper.
        //
        for (long k = 0; k <= N; k++)
        {
            readySteps++;

            if ((readySteps % readyLimit) == 0)
                ThrowIfNotReady(interpreter);

            long e = N - k;

            for (int index = 0; index < J.Length; index++)
            {
                long D = (8L * k) + J[index];
                int c = C[index];

                BigInteger Dbig = new BigInteger(D);
                BigInteger sWide;

                if (e >= s)
                {
                    //
                    // NOTE: m = (c * 16^(e - s)) mod D, computed with a
                    //       SMALL modulus (D fits in a machine word for
                    //       any plausible "k").  Then sWide is just a
                    //       (4*s)-bit shift of "m".
                    //
                    BigInteger powMod = BigInteger.ModPow(
                        Sixteen, new BigInteger(e - s), Dbig);

                    BigInteger m = (ModCanonical(c, Dbig) * powMod) % Dbig;

                    sWide = m << (4 * s);
                }
                else
                {
                    //
                    // NOTE: e < s, so c * 16^e is small (at most 4 *
                    //       16^(s-1)) and strictly less than D * 16^s
                    //       for any D >= 1.  No wide reduction is needed
                    //       beyond canonicalizing a possibly-negative c.
                    //
                    int shift = 4 * (int)e;

                    sWide = new BigInteger(c) << shift;

                    if (sWide.Sign < 0)
                        sWide += (Dbig << (4 * s));
                }

                //
                // NOTE: Split sWide into the scale-units quotient "q" and
                //       the residue "r" in [0, D) in a single division.
                //
                BigInteger r;
                BigInteger q = BigInteger.DivRem(sWide, Dbig, out r);

                sumModScale += q;

                if (sumModScale >= scale)
                    sumModScale %= scale;

                carryScaled += RoundDivToNearest(r * KSmall, Dbig);
            }
        }

        //
        // NOTE: Tail terms (k > N): the per-term magnitude is
        //       c / (16^t * (8*(N + t) + j)), a small fraction.  Add its
        //       rounded value (in carry units) to the carry tracker.  We
        //       maintain "p16" incrementally to avoid re-shifting from
        //       BigInteger.One on every iteration.
        //
        BigInteger p16 = Sixteen;

        for (int t = 1; t <= tailTerms; t++)
        {
            readySteps++;

            if ((readySteps % readyLimit) == 0)
                ThrowIfNotReady(interpreter);

            long offset = N + t;

            for (int index = 0; index < J.Length; index++)
            {
                BigInteger denom = p16 * ((8L * offset) + J[index]);
                BigInteger numer = new BigInteger(C[index]) * KSmall;

                carryScaled += RoundDivToNearest(numer, denom);
            }

            p16 <<= 4;
        }

        //
        // NOTE: Fold the integer part of the carry back into the scale-
        //       units sum, normalize, and drop the guard digits.
        //
        BigInteger K = FloorDiv(carryScaled, KSmall);
        BigInteger remainder = (sumModScale + K) % scale;

        if (remainder.Sign < 0)
            remainder += scale;

        if (localGuardDigits > 0)
            remainder >>= (4 * localGuardDigits);

        string result = ToUpperHex(remainder);
        int length = result.Length;

        if (length < count)
        {
            //
            // NOTE: Pad on the left so the requested digit count is
            //       returned even when the leading hex digits are zero.
            //
            result = new string(
                Characters.Zero, count - length) + result;
        }
        else if (length > count)
        {
            //
            // NOTE: BigInteger's "X" formatter can prepend a leading "0"
            //       to disambiguate sign for values whose high byte has
            //       the high bit set; that (and any genuine extra hex
            //       digits) are stripped here.
            //
            result = result.Substring(length - count, count);
        }

        return result;
    }

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: Returns the single hex digit (as a value in [0, 15]) of pi at
    //       one-based position "startIndex".
    //
    /// <summary>
    /// Computes the single hexadecimal digit of the fractional part of pi at
    /// the one-based position <paramref name="startIndex" />, returned as a
    /// value in the range zero through fifteen.
    /// </summary>
    /// <param name="interpreter">
    /// The interpreter polled for cancellation, or null for none.
    /// </param>
    /// <param name="startIndex">
    /// The one-based position of the hexadecimal digit to return.
    /// </param>
    /// <returns>
    /// The hexadecimal digit value, in the range zero through fifteen.
    /// </returns>
    public static int GetDigit(
        Interpreter interpreter, /* in: OPTIONAL */
        long startIndex          /* in */
        )
    {
        string s = GetDigits(interpreter, startIndex, 1, null);
        char c = s[0];

        if ((c >= Characters.Zero) && (c <= Characters.Nine))
            return c - Characters.Zero;

        return 10 + (c - Characters.A);
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Private Methods
    //
    // NOTE: Throws a ScriptException if the supplied interpreter (if any)
    //       has been asked to cancel or is otherwise not ready.
    //
    /// <summary>
    /// Throws a <see cref="ScriptException" /> when the supplied interpreter,
    /// if any, has been asked to cancel or is otherwise not ready to
    /// continue.
    /// </summary>
    /// <param name="interpreter">
    /// The interpreter to check for readiness, or null for none.
    /// </param>
    /// <exception cref="ScriptException">
    /// Thrown when the interpreter is not ready to continue.
    /// </exception>
    private static void ThrowIfNotReady(
        Interpreter interpreter /* in: OPTIONAL */
        )
    {
        Result error = null;

        if (!IsReady(interpreter, ref error))
            throw new ScriptException(error);
    }

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: Returns true when the supplied interpreter is null (no
    //       cancellation context) or reports itself ready to continue;
    //       otherwise sets "error" to the readiness failure and returns
    //       false.
    //
    /// <summary>
    /// Determines whether the computation may continue, which is the case when
    /// the supplied interpreter is null or reports itself ready.
    /// </summary>
    /// <param name="interpreter">
    /// The interpreter to check for readiness, or null for none.
    /// </param>
    /// <param name="error">
    /// Upon a not-ready result, receives the readiness failure.
    /// </param>
    /// <returns>
    /// Non-zero when the computation may continue; otherwise, zero.
    /// </returns>
    private static bool IsReady(
        Interpreter interpreter, /* in: OPTIONAL */
        ref Result error         /* out */
        )
    {
        return (interpreter == null) || (Interpreter.Ready(
            interpreter, null, ref error) == ReturnCode.Ok);
    }

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: Returns "value" reduced modulo "modulus", with the result in
    //       the canonical non-negative range [0, modulus).
    //
    /// <summary>
    /// Reduces the supplied value modulo the supplied modulus, returning the
    /// result in the canonical non-negative range from zero up to (but not
    /// including) the modulus.
    /// </summary>
    /// <param name="value">
    /// The value to reduce.
    /// </param>
    /// <param name="modulus">
    /// The modulus to reduce by.
    /// </param>
    /// <returns>
    /// The non-negative residue of the value modulo the modulus.
    /// </returns>
    private static BigInteger ModCanonical(
        int value,         /* in */
        BigInteger modulus /* in */
        )
    {
        BigInteger v = new BigInteger(value);

        v %= modulus;

        if (v.Sign < 0)
            v += modulus;

        return v;
    }

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: Returns n divided by d, rounded to the nearest integer with
    //       ties broken by rounding away from zero.  Assumes d > 0.
    //
    /// <summary>
    /// Divides <paramref name="n" /> by <paramref name="d" /> and rounds the
    /// result to the nearest integer, with ties broken by rounding away from
    /// zero.  The divisor is assumed to be positive.
    /// </summary>
    /// <param name="n">
    /// The dividend.
    /// </param>
    /// <param name="d">
    /// The (positive) divisor.
    /// </param>
    /// <returns>
    /// The quotient rounded to the nearest integer.
    /// </returns>
    private static BigInteger RoundDivToNearest(
        BigInteger n, /* in */
        BigInteger d  /* in */
        )
    {
        if (n.Sign >= 0)
            return (n + (d >> 1)) / d;

        return -(((-n) + (d >> 1)) / d);
    }

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: Returns the mathematical floor of n / d, even when the signs
    //       of n and d differ.  BigInteger.DivRem truncates toward zero;
    //       this adjusts toward negative infinity when the remainder is
    //       non-zero and the signs disagree.
    //
    /// <summary>
    /// Computes the mathematical floor of <paramref name="n" /> divided by
    /// <paramref name="d" />, rounding toward negative infinity even when the
    /// signs of the operands differ.
    /// </summary>
    /// <param name="n">
    /// The dividend.
    /// </param>
    /// <param name="d">
    /// The divisor.
    /// </param>
    /// <returns>
    /// The floor of the quotient.
    /// </returns>
    private static BigInteger FloorDiv(
        BigInteger n, /* in */
        BigInteger d  /* in */
        )
    {
        BigInteger r;
        BigInteger q = BigInteger.DivRem(n, d, out r);

        if ((r != 0) && ((n.Sign < 0) ^ (d.Sign < 0)))
            q -= BigInteger.One;

        return q;
    }

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: Returns the smallest power of two greater than or equal to
    //       "value", with a floor of one for non-positive inputs.
    //
    /// <summary>
    /// Returns the smallest power of two greater than or equal to the supplied
    /// value, with a floor of one for non-positive inputs.
    /// </summary>
    /// <param name="value">
    /// The value to round up to a power of two.
    /// </param>
    /// <returns>
    /// The smallest power of two at least as large as the value.
    /// </returns>
    private static BigInteger NextPowerOfTwo(
        BigInteger value /* in */
        )
    {
        if (value <= BigInteger.One)
            return BigInteger.One;

        if ((value & (value - BigInteger.One)) == BigInteger.Zero)
            return value;

        int length = BitLength(value);

        return BigInteger.One << length;
    }

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: Returns the number of bits needed to represent the positive
    //       BigInteger "value", i.e. floor(log2(value)) + 1.  Returns
    //       zero for non-positive inputs.
    //
    /// <summary>
    /// Returns the number of bits needed to represent the supplied positive
    /// value (the floor of its base-two logarithm plus one), or zero for
    /// non-positive inputs.
    /// </summary>
    /// <param name="value">
    /// The value whose bit length is computed.
    /// </param>
    /// <returns>
    /// The number of bits needed to represent the value.
    /// </returns>
    private static int BitLength(
        BigInteger value /* in */
        )
    {
        if (value.Sign <= 0)
            return 0;

        byte[] bytes = value.ToByteArray();
        int index = bytes.Length - 1;

        //
        // NOTE: BigInteger.ToByteArray appends a zero byte for positive
        //       values whose high bit would otherwise indicate sign;
        //       skip those leading zero bytes.
        //
        while ((index > 0) && (bytes[index] == 0))
            index--;

        byte msb = bytes[index];
        int msbIndex = 7;

        while ((msbIndex >= 0) && ((msb & (1 << msbIndex)) == 0))
            msbIndex--;

        return index * 8 + (msbIndex + 1);
    }

    ///////////////////////////////////////////////////////////////////////////

    //
    // NOTE: Returns the uppercase hexadecimal representation of "value"
    //       (sign byte stripping is handled by the caller's substring
    //       logic in GetDigits).
    //
    /// <summary>
    /// Returns the uppercase hexadecimal representation of the supplied value;
    /// any sign-byte stripping is handled by the caller.
    /// </summary>
    /// <param name="value">
    /// The value to format.
    /// </param>
    /// <returns>
    /// The uppercase hexadecimal representation of the value.
    /// </returns>
    private static string ToUpperHex(
        BigInteger value /* in */
        )
    {
        if (value.IsZero)
            return Characters.Zero.ToString();

        return value.ToString(Characters.X.ToString());
    }
    #endregion
}
