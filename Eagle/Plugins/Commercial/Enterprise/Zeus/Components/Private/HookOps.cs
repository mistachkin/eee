/*
 * HookOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if !NATIVE
#error "This file cannot be compiled or used properly with native code disabled."
#endif

#if !EMIT
#error "This file cannot be compiled or used properly with EMIT disabled."
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security;

#if !NET_40
using System.Security.Permissions;
#endif

using System.Threading;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Constants;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

using UNM = Zeus.Components.Private.HookOps.UnsafeNativeMethods;
using _Arch = Eagle._Components.Public.ProcessorArchitecture;
using PatchPair = Eagle._Components.Public.AnyPair<int, byte[]>;

using JumpKey = Eagle._Components.Public.AnyPair<
    Eagle._Components.Public.ProcessorArchitecture,
    Zeus.Components.Private.PatchKind>;

using JumpPair = Eagle._Components.Public.AnyPair<
    Eagle._Components.Public.AnyPair<int, byte[]>,
    Eagle._Components.Public.AnyPair<int, byte[]>>;

using PatchDictionary = System.Collections.Generic.Dictionary<
    Eagle._Components.Public.ProcessorArchitecture,
    Eagle._Components.Public.AnyPair<int, byte[]>>;

using JumpStubDictionary = System.Collections.Generic.Dictionary<
    Eagle._Components.Public.AnyPair<
        Eagle._Components.Public.ProcessorArchitecture,
        Zeus.Components.Private.PatchKind>,
    Eagle._Components.Public.AnyPair<
        Eagle._Components.Public.AnyPair<int, byte[]>,
        Eagle._Components.Public.AnyPair<int, byte[]>>>;

using PageRangeList = System.Collections.Generic.List<
    Zeus.Components.Private.HookOps.PageRange>;

#if NET_STANDARD_21
using Index = Eagle._Constants.Index;
#endif

namespace Zeus.Components.Private
{
    #region Patch Kind Enumeration
    /// <summary>
    /// Identifies the kind of native code patch used to redirect a hooked
    /// method: a full trampoline, an absolute-address jump stub, or a
    /// relative-address jump stub.
    /// </summary>
    [ObjectId("c6cb7d20-8a2f-4c52-95bd-8b392b669efc")]
    internal enum PatchKind
    {
        /// <summary>
        /// No patch kind.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// An invalid patch kind.
        /// </summary>
        Invalid = 0x1,
        /// <summary>
        /// A legacy full-trampoline patch that directly replaces the method's
        /// entry-point code.
        /// </summary>
        FullTrampoline = 0x100,
        /// <summary>
        /// A jump-stub patch that uses a 64-bit absolute target address.
        /// </summary>
        AbsoluteAddress = 0x200,
        /// <summary>
        /// A jump-stub patch that uses a 32-bit relative target offset.
        /// </summary>
        RelativeAddress = 0x400
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    #region Patch Flags Enumeration
    /// <summary>
    /// Controls optional behaviors when patching a method, such as allowing
    /// legacy patching semantics and allowing a fallback when a jump-stub
    /// sequence cannot be recognized.
    /// </summary>
    [Flags()]
    [ObjectId("f766f6c6-90eb-4490-9904-c0d2b8f5015b")]
    internal enum PatchFlags
    {
        /// <summary>
        /// No patch flags.
        /// </summary>
        None = 0x0,
        /// <summary>
        /// Invalid patch flags.
        /// </summary>
        Invalid = 0x1,
        /// <summary>
        /// Allow legacy (full-trampoline) patching semantics.
        /// </summary>
        AllowLegacy = 0x2,
        /// <summary>
        /// Allow a fallback when an unrecognized jump-stub sequence is
        /// encountered.
        /// </summary>
        AllowFallback = 0x4,
        /// <summary>
        /// The combination of flags used by the self-test (allow legacy and
        /// allow fallback).
        /// </summary>
        SelfTest = AllowLegacy | AllowFallback
    }
    #endregion

    ///////////////////////////////////////////////////////////////////////////

    /// <summary>
    /// Provides the CLR method hooking engine for the Zeus plugin.  It patches
    /// managed methods at the native code level so calls are redirected to a
    /// replacement method, supporting x86, x64, ARM, and ARM64 across Windows,
    /// macOS, and Linux via full-trampoline and jump-stub strategies.  Hooks
    /// are tracked by a <see cref="HookClientData" /> object whose disposal
    /// reverses the hook.
    /// </summary>
#if NET_40
    [SecurityCritical()]
#else
    [SecurityPermission(SecurityAction.LinkDemand, UnmanagedCode = true)]
#endif
    [ObjectId("b2e6ec59-dbba-4874-973a-c4b2f4cf76ad")]
    internal static class HookOps
    {
        #region Hook Data Helper Class
        /// <summary>
        /// Holds the immutable state describing a single installed hook: the
        /// processor architecture, the old and new methods and their handles
        /// and code pointers, the patch kind, the saved and applied patch
        /// bytes, and whether the hook is currently active.
        /// </summary>
        [ObjectId("58694823-2e20-4244-be75-cbe510407c29")]
        internal sealed class HookData
        {
            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="HookData" /> instance capturing the
            /// full state of a hook.
            /// </summary>
            /// <param name="processor">
            /// The processor architecture of the hooked method.
            /// </param>
            /// <param name="oldMethod">
            /// The original (hooked) method.
            /// </param>
            /// <param name="newMethod">
            /// The replacement method.
            /// </param>
            /// <param name="oldHandle">
            /// The runtime method handle of the original method.
            /// </param>
            /// <param name="newHandle">
            /// The runtime method handle of the replacement method.
            /// </param>
            /// <param name="oldPointer">
            /// The native code pointer of the original method.
            /// </param>
            /// <param name="newPointer">
            /// The native code pointer of the replacement method.
            /// </param>
            /// <param name="kind">
            /// The kind of patch applied.
            /// </param>
            /// <param name="savedPatch">
            /// The original bytes saved before patching.
            /// </param>
            /// <param name="applyPatch">
            /// The patch bytes that were applied.
            /// </param>
            /// <param name="active">
            /// Non-zero if the hook is currently active.
            /// </param>
            public HookData(
                _Arch processor,               /* in */
                MethodBase oldMethod,          /* in */
                MethodBase newMethod,          /* in */
                RuntimeMethodHandle oldHandle, /* in */
                RuntimeMethodHandle newHandle, /* in */
                IntPtr oldPointer,             /* in */
                IntPtr newPointer,             /* in */
                PatchKind kind,                /* in */
                byte[] savedPatch,             /* in */
                byte[] applyPatch,             /* in */
                bool active                    /* in */
                )
            {
                this.processor = processor;
                this.oldMethod = oldMethod;
                this.newMethod = newMethod;
                this.oldHandle = oldHandle;
                this.newHandle = newHandle;
                this.oldPointer = oldPointer;
                this.newPointer = newPointer;
                this.kind = kind;
                this.savedPatch = savedPatch;
                this.applyPatch = applyPatch;
                this.active = active;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Properties
            /// <summary>
            /// The backing field for the <see cref="Processor" /> property.
            /// </summary>
            private readonly _Arch processor;
            /// <summary>
            /// Gets the processor architecture of the hooked method.
            /// </summary>
            public _Arch Processor
            {
                get { return processor; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="OldMethod" /> property.
            /// </summary>
            private readonly MethodBase oldMethod;
            /// <summary>
            /// Gets the original (hooked) method.
            /// </summary>
            public MethodBase OldMethod
            {
                get { return oldMethod; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="NewMethod" /> property.
            /// </summary>
            private readonly MethodBase newMethod;
            /// <summary>
            /// Gets the replacement method.
            /// </summary>
            public MethodBase NewMethod
            {
                get { return newMethod; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="OldHandle" /> property.
            /// </summary>
            private readonly RuntimeMethodHandle oldHandle;
            /// <summary>
            /// Gets the runtime method handle of the original method.
            /// </summary>
            public RuntimeMethodHandle OldHandle
            {
                get { return oldHandle; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="NewHandle" /> property.
            /// </summary>
            private readonly RuntimeMethodHandle newHandle;
            /// <summary>
            /// Gets the runtime method handle of the replacement method.
            /// </summary>
            public RuntimeMethodHandle NewHandle
            {
                get { return newHandle; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="OldPointer" /> property.
            /// </summary>
            private readonly IntPtr oldPointer;
            /// <summary>
            /// Gets the native code pointer of the original method.
            /// </summary>
            public IntPtr OldPointer
            {
                get { return oldPointer; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="NewPointer" /> property.
            /// </summary>
            private readonly IntPtr newPointer;
            /// <summary>
            /// Gets the native code pointer of the replacement method.
            /// </summary>
            public IntPtr NewPointer
            {
                get { return newPointer; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="Kind" /> property.
            /// </summary>
            private readonly PatchKind kind;
            /// <summary>
            /// Gets the kind of patch applied.
            /// </summary>
            public PatchKind Kind
            {
                get { return kind; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="SavedPatch" /> property.
            /// </summary>
            private readonly byte[] savedPatch;
            /// <summary>
            /// Gets the original bytes saved before patching.
            /// </summary>
            public byte[] SavedPatch
            {
                get { return savedPatch; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="ApplyPatch" /> property.
            /// </summary>
            private readonly byte[] applyPatch;
            /// <summary>
            /// Gets the patch bytes that were applied.
            /// </summary>
            public byte[] ApplyPatch
            {
                get { return applyPatch; }
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The backing field for the <see cref="Active" /> property.
            /// </summary>
            private bool active;
            /// <summary>
            /// Gets or sets a value indicating whether the hook is currently
            /// active.
            /// </summary>
            public bool Active
            {
                get { return active; }
                set { active = value; }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region System.Object Overrides
            /// <summary>
            /// Returns a string that represents this instance.
            /// </summary>
            /// <returns>
            /// A string that represents this instance.
            /// </returns>
            public override string ToString()
            {
                return RuntimeHelpers.GetHashCode(this).ToString();
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Hook Client Data Helper Class
        /// <summary>
        /// Wraps a <see cref="HookData" /> as disposable caller data;
        /// disposing it stops (reverses) the associated hook.
        /// </summary>
        internal sealed class HookClientData : ClientData, IDisposable
        {
            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="HookClientData" /> instance
            /// wrapping the specified hook data.
            /// </summary>
            /// <param name="hookData">
            /// The hook data to wrap.
            /// </param>
            /// <param name="data">
            /// The additional caller data, if any.
            /// </param>
            public HookClientData(
                HookData hookData, /* in */
                object data        /* in */
                )
                : base(data, true)
            {
                this.hookData = hookData;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Properties
            /// <summary>
            /// The backing field for the <see cref="HookData" /> property.
            /// </summary>
            private HookData hookData;
            /// <summary>
            /// Gets the hook data wrapped by this instance.
            /// </summary>
            public HookData HookData
            {
                get { CheckDisposed(); return hookData; }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region System.Object Overrides
            /// <summary>
            /// Returns a string that represents this instance.
            /// </summary>
            /// <returns>
            /// A string that represents this instance.
            /// </returns>
            public override string ToString()
            {
                CheckDisposed();

                return (hookData != null) ? hookData.ToString() : null;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IDisposable Members
            /// <summary>
            /// Releases the resources used by this instance, stopping the
            /// associated hook.
            /// </summary>
            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region IDisposable "Pattern" Members
            /// <summary>
            /// Non-zero if this instance has been disposed.
            /// </summary>
            private bool disposed;
            /// <summary>
            /// Throws an exception if this instance has already been disposed.
            /// </summary>
            /// <exception cref="ObjectDisposedException">
            /// Thrown if this instance has been disposed and disposed-object
            /// checking is enabled.
            /// </exception>
            private void CheckDisposed() /* throw */
            {
#if THROW_ON_DISPOSED
                if (disposed && Engine.IsThrowOnDisposed(null, null))
                {
                    throw new ObjectDisposedException(
                        typeof(HookClientData).Name);
                }
#endif
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Releases the resources used by this instance, stopping the
            /// associated hook when disposing.
            /// </summary>
            /// <param name="disposing">
            /// Non-zero if called from <see cref="IDisposable.Dispose" />;
            /// zero if called from the finalizer.
            /// </param>
            private /* protected virtual */ void Dispose(
                bool disposing /* in */
                )
            {
                if (!disposed)
                {
                    if (disposing)
                    {
                        ////////////////////////////////////
                        // dispose managed resources here...
                        ////////////////////////////////////

                        IClientData clientData = this;
                        ReturnCode stopCode;
                        Result stopError = null;

                        stopCode = Stop(
                            ref clientData, ref stopError);

                        if (stopCode != ReturnCode.Ok)
                        {
                            Utility.Complain(
                                null, stopCode, stopError);
                        }
                    }

                    //////////////////////////////////////
                    // release unmanaged resources here...
                    //////////////////////////////////////

                    disposed = true;
                }
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Destructor
            /// <summary>
            /// Finalizes an instance of the <see cref="HookClientData" />
            /// class.
            /// </summary>
            ~HookClientData()
            {
                Dispose(false);
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Page Range Helper Class
        /// <summary>
        /// Describes a contiguous range of memory pages by its start and end
        /// addresses and protection flags.
        /// </summary>
        [ObjectId("46d86f05-7282-4d57-81b1-4f912b2bb1be")]
        internal struct PageRange
        {
            #region Private Data
            /// <summary>
            /// The start address of the page range.
            /// </summary>
            internal IntPtr startAddress;
            /// <summary>
            /// The end address of the page range.
            /// </summary>
            internal IntPtr endAddress;
            /// <summary>
            /// The protection flags of the page range.
            /// </summary>
            internal int protection;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Constructors
            /// <summary>
            /// Constructs a new <see cref="PageRange" /> describing a
            /// contiguous range of memory pages.
            /// </summary>
            /// <param name="startAddress">
            /// The start address of the range.
            /// </param>
            /// <param name="endAddress">
            /// The end address of the range.
            /// </param>
            /// <param name="protection">
            /// The protection flags of the range.
            /// </param>
            public PageRange(
                IntPtr startAddress, /* in */
                IntPtr endAddress,   /* in */
                int protection       /* in */
                )
            {
                this.startAddress = startAddress;
                this.endAddress = endAddress;
                this.protection = protection;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region ARM64 Helper Class
        /// <summary>
        /// Provides ARM64 instruction decoding helpers used to follow
        /// jump-stub sequences (branches, literal loads, and ADRP/ADD/LDR
        /// address computations) to the final target address.
        /// </summary>
        [ObjectId("e229f479-e07a-4a1b-821d-18d525149aca")]
        private static class ARM64
        {
            #region Private Constants
            //
            // NOTE: 4KB alignment mask; ADRP operates on page granularity.
            //
            /// <summary>
            /// The 4KB page alignment mask used by ADRP, which operates on
            /// page granularity.
            /// </summary>
            private const long PageMask = ~0xFFF;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Mask covering the 6-bit major opcode in bits [31:26].
            //       Bit 31 IS included so that, combined with OpB, only B
            //       matches and BL does not: BL is a call (it writes the
            //       return address to the link register) rather than an
            //       unconditional tail-branch, so following its target as a
            //       redirect would be incorrect.
            //
            /// <summary>
            /// The mask covering the 6-bit major opcode bits [31:26], so that
            /// (with OpB) it matches the B instruction only, not BL.
            /// </summary>
            private const uint OpMask = 0xFC000000;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Pattern for B (unconditional branch).
            //
            /// <summary>
            /// The bit pattern for the B (unconditional branch) instruction.
            /// </summary>
            private const uint OpB = 0x14000000;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Mask for extracting 2-bit value.
            //
            /// <summary>
            /// The mask for extracting a 2-bit value.
            /// </summary>
            private const uint Imm2Mask = 0x3;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Mask for extracting 5-bit value.
            //
            /// <summary>
            /// The mask for extracting a 5-bit value.
            /// </summary>
            private const uint Imm5Mask = 0x1F;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Mask for extracting 12-bit value.
            //
            /// <summary>
            /// The mask for extracting a 12-bit value.
            /// </summary>
            private const uint Imm12Mask = 0xFFF;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Mask for extracting 19-bit value.
            //
            /// <summary>
            /// The mask for extracting a 19-bit value.
            /// </summary>
            private const uint Imm19Mask = 0x7FFFF;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Mask for extracting 26-bit value.
            //
            /// <summary>
            /// The mask for extracting a 26-bit value.
            /// </summary>
            private const uint Imm26Mask = 0x03FFFFFF;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Mask that clears the Rn field (bits [9:5]) and keeps fixed
            //       opcode bits.
            //
            /// <summary>
            /// The mask that clears the Rn field while keeping the fixed BR
            /// opcode bits.
            /// </summary>
            private const uint BrMask = 0xFFFFFC1F;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Fixed bits for BR <Xn>.
            //
            /// <summary>
            /// The fixed bits for the BR &lt;Xn&gt; instruction.
            /// </summary>
            private const uint BrOp = 0xD61F0000;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Coarse mask to detect the LDR (literal) family quickly.
            //
            /// <summary>
            /// The coarse mask used to quickly detect the LDR (literal)
            /// family.
            /// </summary>
            private const uint LdrLitMask = 0xFF000000;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Base opcode for LDR (literal) Xt.
            //
            /// <summary>
            /// The base opcode for LDR (literal) Xt.
            /// </summary>
            private const uint LdrLitOp = 0x58000000;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Mask for ADRP/ADR group with imm + Rd.
            //
            /// <summary>
            /// The mask for the ADRP/ADR group with an immediate and Rd.
            /// </summary>
            private const uint AdrpMask = 0x9F000000;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Base opcode for ADRP.
            //
            /// <summary>
            /// The base opcode for ADRP.
            /// </summary>
            private const uint AdrpOp = 0x90000000;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Mask covering opcode and shift; we accept only shift==0.
            //
            /// <summary>
            /// The mask covering the opcode and shift, accepting only a zero
            /// shift.
            /// </summary>
            private const uint AddMask = 0xFFC00000;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Base opcode: ADD (immediate, 64-bit, shift=0).
            //
            /// <summary>
            /// The base opcode for ADD (immediate, 64-bit, shift zero).
            /// </summary>
            private const uint AddOp = 0x91000000;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Mask for the unsigned-offset LDR (64-bit) form (no pre /
            //       post-index).
            //
            /// <summary>
            /// The mask for the unsigned-offset 64-bit LDR form.
            /// </summary>
            private const uint LdrUMask = 0xFFC00000;

            ///////////////////////////////////////////////////////////////////////

            //
            // NOTE: Base opcode for LDR Xt, [Xn, #imm12] (64-bit).
            //
            /// <summary>
            /// The base opcode for LDR Xt, [Xn, #imm12] (64-bit).
            /// </summary>
            private const uint LdrUOp = 0xF9400000;
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Helper Methods
            /// <summary>
            /// Adds a signed byte offset to an address.
            /// </summary>
            /// <param name="address">
            /// The base address.
            /// </param>
            /// <param name="offset">
            /// The signed offset to add.
            /// </param>
            /// <returns>
            /// The resulting address.
            /// </returns>
            private static IntPtr Add(
                IntPtr address, /* in */
                long offset     /* in */
                )
            {
                return new IntPtr(address.ToInt64() + offset);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Returns the page-aligned base address containing the given
            /// address.
            /// </summary>
            /// <param name="address">
            /// The address to align.
            /// </param>
            /// <returns>
            /// The page-aligned base address.
            /// </returns>
            private static IntPtr PageOf(
                IntPtr address /* in */
                )
            {
                return new IntPtr(address.ToInt64() & PageMask);
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Reads a 64-bit unsigned value from the target process memory at
            /// the given address.
            /// </summary>
            /// <param name="process">
            /// A handle to the process whose memory is accessed.
            /// </param>
            /// <param name="address">
            /// The target memory address.
            /// </param>
            /// <param name="value">
            /// Upon success, receives the value read.
            /// </param>
            /// <param name="error">
            /// Upon failure, receives an error message describing the problem.
            /// </param>
            /// <returns>
            /// Non-zero on success; otherwise, zero.
            /// </returns>
            private static bool ReadU64(
                IntPtr process,  /* in */
                IntPtr address,  /* in */
                out ulong value, /* out */
                ref Result error /* out */
                )
            {
                byte[] buffer = new byte[sizeof(ulong)];

                if (Read(
                        process, address, buffer,
                        ref error) != ReturnCode.Ok)
                {
                    value = 0;
                    return false;
                }

                value = BitConverter.ToUInt64(buffer, 0);
                return true;
            }

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Reads instruction words, one at a time, into the supplied
            /// buffer until at least the requested number have been read.
            /// Reading a single word at a time (instead of a fixed-size
            /// block) allows a short stub near an unmapped page boundary to
            /// be decoded without the read failing because it spanned into
            /// unmapped memory.
            /// </summary>
            /// <param name="process">
            /// A handle to the process whose memory is accessed.
            /// </param>
            /// <param name="address">
            /// The base address of the instruction sequence.
            /// </param>
            /// <param name="instructions">
            /// The buffer that receives the decoded instruction words.
            /// </param>
            /// <param name="read">
            /// On input, the number of words already read; on output, the
            /// number of words read so far (never decreased).
            /// </param>
            /// <param name="need">
            /// The total number of words that must be available.
            /// </param>
            /// <param name="error">
            /// Upon failure, receives an error message describing the problem.
            /// </param>
            /// <returns>
            /// Non-zero once at least <paramref name="need" /> words have been
            /// read; otherwise, zero (e.g. when a read failed).
            /// </returns>
            private static bool ReadMoreInstructions(
                IntPtr process,      /* in */
                IntPtr address,      /* in */
                uint[] instructions, /* in, out */
                ref int read,        /* in, out */
                int need,            /* in */
                ref Result error     /* out */
                )
            {
                while (read < need)
                {
                    byte[] buffer = new byte[sizeof(uint)];

                    if (Read(
                            process, Add(address, read * sizeof(uint)),
                            buffer, ref error) != ReturnCode.Ok)
                    {
                        return false;
                    }

                    instructions[read] = BitConverter.ToUInt32(buffer, 0);
                    read++;
                }

                return true;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Private Decoder Methods
            // Decodes: B <rel26> (unconditional branch).  Only B matches;
            //          BL (bit 31 set) is rejected because it is a call, not
            //          an unconditional tail-branch (see OpMask).
            // Encoding: 31..26 = 0b000101 ; imm26 = bits[25:0] ;
            //           target = PC + signExtend(imm26<<2)
            // Why: Many stubs are a single B to the real entry-point;
            //      we can just follow it.
            /// <summary>
            /// Decodes a B (unconditional branch) instruction, returning its
            /// PC-relative byte offset.
            /// </summary>
            /// <param name="instruction">
            /// The 32-bit instruction word to decode.
            /// </param>
            /// <param name="offset">
            /// Upon success, receives the branch offset in bytes.
            /// </param>
            /// <returns>
            /// Non-zero if the instruction is a B; otherwise, zero.
            /// </returns>
            private static bool TryDecode_B(
                uint instruction, /* in */
                out long offset   /* out */
                )
            {
                if ((instruction & OpMask) != OpB)
                {
                    offset = 0;
                    return false;
                }

                int imm26 = (int)(instruction & Imm26Mask);

                offset = (long)(imm26 << 6) >> 4;
                return true;
            }

            ///////////////////////////////////////////////////////////////////

            // Decodes: BR Xn  (branch to register)
            // Encoding: 1101011 000011111 0000 00 Rn 00000
            //           -> 0xD61F0000 | (Rn<<5)
            // Why: We need to ensure LDR (literal) / ADRP+ADD / LDR
            //      build the target in the *same* register used by BR.
            /// <summary>
            /// Decodes a BR Xn (branch to register) instruction, returning the
            /// register number it branches through.
            /// </summary>
            /// <param name="instruction">
            /// The 32-bit instruction word to decode.
            /// </param>
            /// <param name="register">
            /// Upon success, receives the branch register number.
            /// </param>
            /// <returns>
            /// Non-zero if the instruction is a BR; otherwise, zero.
            /// </returns>
            private static bool TryDecode_Br_X(
                uint instruction, /* in */
                out int register  /* out */
                )
            {
                if ((instruction & BrMask) != BrOp)
                {
                    register = 0;
                    return false;
                }

                register = (int)((instruction >> 5) & Imm5Mask);
                return true;
            }

            ///////////////////////////////////////////////////////////////////

            // Decodes: LDR (literal) Xt, [PC, #imm19]
            // Encoding: 0b01x1 1000 000 imm19 Rt (for Xt)
            //           => op family: 0x58000000
            // Result: Rt (target reg), disp = signExtend(imm19) << 2 ;
            //         base PC = address of this LDR (not PC+4).
            // Why: This is used by stubs that load an absolute pointer
            //      from a PC-relative literal slot.
            /// <summary>
            /// Decodes an LDR (literal) Xt instruction, returning the target
            /// register and the PC-relative byte offset of the literal.
            /// </summary>
            /// <param name="instruction">
            /// The 32-bit instruction word to decode.
            /// </param>
            /// <param name="register">
            /// Upon success, receives the target register number.
            /// </param>
            /// <param name="offset">
            /// Upon success, receives the literal byte offset.
            /// </param>
            /// <returns>
            /// Non-zero if the instruction is an LDR literal; otherwise, zero.
            /// </returns>
            private static bool TryDecode_Ldr_Literal_X(
                uint instruction, /* in */
                out int register, /* out */
                out long offset   /* out */
                )
            {
                if ((instruction & LdrLitMask) != LdrLitOp)
                {
                    register = 0;
                    offset = 0;
                    return false;
                }

                int imm19 = (int)((instruction >> 5) & Imm19Mask);

                register = (int)(instruction & Imm5Mask);
                offset = (long)(imm19 << 13) >> 11;

                return true;
            }

            ///////////////////////////////////////////////////////////////////

            // Decodes: ADRP Xd, #imm (PC-relative page)
            // Encoding: 0b1 00 immLow[1:0] immHigh[18:0] Rd
            //           => base: 0x90000000
            // Result: Rd, pageDisp = signExtend((immHigh:immLow) << 12)
            // Why: Common prelude to compute an absolute address near the
            //      current 4KB page.
            /// <summary>
            /// Decodes an ADRP Xd instruction, returning the destination
            /// register and the PC-relative page byte offset.
            /// </summary>
            /// <param name="instruction">
            /// The 32-bit instruction word to decode.
            /// </param>
            /// <param name="register">
            /// Upon success, receives the destination register number.
            /// </param>
            /// <param name="offset">
            /// Upon success, receives the page offset in bytes.
            /// </param>
            /// <returns>
            /// Non-zero if the instruction is an ADRP; otherwise, zero.
            /// </returns>
            private static bool TryDecode_Adrp(
                uint instruction, /* in */
                out int register, /* out */
                out long offset   /* out */
                )
            {
                if ((instruction & AdrpMask) != AdrpOp)
                {
                    register = 0;
                    offset = 0;
                    return false;
                }

                int immLow = (int)((instruction >> 29) & Imm2Mask);
                int immHigh = (int)((instruction >> 5) & Imm19Mask);

#pragma warning disable 675 // NOTE: Purposely sign-extended.
                long imm21 = ((long)immHigh << 2) | immLow;
#pragma warning restore 675

                long signed = (imm21 << 43) >> 43;

                register = (int)(instruction & Imm5Mask);
                offset = signed << 12;

                return true;
            }

            ///////////////////////////////////////////////////////////////////

            // Decodes: ADD Xd, Xn, #imm12 (64-bit, immediate)
            // Encoding: base 0x91000000 (shift=0 form) ; we only accept
            //           shift==0 (bytes), not <<12 form.
            // Result: verifies Xd==expectedRd && Xn==expectedRn; returns
            //         imm12 as *bytes*
            // Why: Stubs often do ADRP ; ADD to get absolute address ;
            //      we need the low 12-bit byte offset.
            /// <summary>
            /// Decodes an ADD Xd, Xn, #imm12 (64-bit) instruction, verifying
            /// the expected registers and returning the immediate as a byte
            /// value.
            /// </summary>
            /// <param name="instruction">
            /// The 32-bit instruction word to decode.
            /// </param>
            /// <param name="wantRegister1">
            /// The expected destination register.
            /// </param>
            /// <param name="wantRegister2">
            /// The expected source register.
            /// </param>
            /// <param name="value">
            /// Upon success, receives the 12-bit immediate value.
            /// </param>
            /// <returns>
            /// Non-zero if the instruction matches; otherwise, zero.
            /// </returns>
            private static bool TryDecode_Add_Imm12(
                uint instruction,  /* in */
                int wantRegister1, /* in */
                int wantRegister2, /* in */
                out int value      /* out */
                )
            {
                if ((instruction & AddMask) != AddOp)
                {
                    value = 0;
                    return false;
                }

                int haveRegister1 = (int)(instruction & Imm5Mask);
                int haveRegister2 = (int)((instruction >> 5) & Imm5Mask);

                if ((haveRegister1 != wantRegister1) ||
                    (haveRegister2 != wantRegister2))
                {
                    value = 0;
                    return false;
                }

                value = (int)((instruction >> 10) & Imm12Mask);
                return true;
            }

            ///////////////////////////////////////////////////////////////////

            // Decodes: LDR Xt, [Xn, #imm12]  (unsigned offset, 64-bit)
            // Encoding: base 0xF9400000 ; scale = 8 bytes (for 64-bit) ;
            //           effective address = Xn + imm12*8
            // Result: verifies Xt==expectedRt && Xn==expectedRn ; returns
            //         imm12 *scaled to bytes*
            // Why: Import thunks often load a 64-bit pointer from a cell:
            //      ADRP/ADD computes base ; LDR reads cell ; BR jumps.
            /// <summary>
            /// Decodes an LDR Xt, [Xn, #imm12] (unsigned offset, 64-bit)
            /// instruction, verifying the expected registers and returning the
            /// immediate scaled to bytes.
            /// </summary>
            /// <param name="instruction">
            /// The 32-bit instruction word to decode.
            /// </param>
            /// <param name="wantRegister1">
            /// The expected target register.
            /// </param>
            /// <param name="wantRegister2">
            /// The expected base register.
            /// </param>
            /// <param name="value">
            /// Upon success, receives the byte-scaled offset.
            /// </param>
            /// <returns>
            /// Non-zero if the instruction matches; otherwise, zero.
            /// </returns>
            private static bool TryDecode_Ldr_X_Unsigned(
                uint instruction,  /* in */
                int wantRegister1, /* in */
                int wantRegister2, /* in */
                out int value      /* out */
                )
            {
                if ((instruction & LdrUMask) != LdrUOp)
                {
                    value = 0;
                    return false;
                }

                int haveRegister1 = (int)(instruction & Imm5Mask);
                int haveRegister2 = (int)((instruction >> 5) & Imm5Mask);

                if ((haveRegister1 != wantRegister1) ||
                    (haveRegister2 != wantRegister2))
                {
                    value = 0;
                    return false;
                }

                int imm12 = (int)((instruction >> 10) & Imm12Mask);

                value = imm12 << 3;
                return true;
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Public Helper Methods
            /// <summary>
            /// Follows an ARM64 jump-stub sequence from the given address to
            /// the final target code address, decoding branch and
            /// address-computation patterns up to the follow limit.  When
            /// fallback is allowed, an unrecognized sequence resolves to the
            /// current address.
            /// </summary>
            /// <param name="process">
            /// A handle to the process whose memory is accessed.
            /// </param>
            /// <param name="maximumFollow">
            /// The maximum number of jump-stubs to follow.
            /// </param>
            /// <param name="allowFallback">
            /// Non-zero to resolve to the current address on an unrecognized
            /// sequence.
            /// </param>
            /// <param name="address">
            /// On input, the starting address; on success, the resolved target
            /// address.
            /// </param>
            /// <param name="kind">
            /// Upon success, receives the resolved patch kind.
            /// </param>
            /// <param name="error">
            /// Upon failure, receives an error message describing the problem.
            /// </param>
            /// <returns>
            /// Non-zero on success; otherwise, zero.
            /// </returns>
            public static bool TryToResolve(
                IntPtr process,     /* in */
                int maximumFollow,  /* in */
                bool allowFallback, /* in */
                ref IntPtr address, /* in, out */
                ref PatchKind kind, /* out */
                ref Result error    /* out */
                )
            {
                IntPtr current = address;

                for (int follow = 0; follow < maximumFollow; follow++)
                {
                    uint[] instructions = new uint[4];
                    int read = 0;

                    //
                    // NOTE: Read the instruction words one at a time, on
                    //       demand, so a short stub (e.g. a lone B) close to
                    //       an unmapped page boundary can still be followed
                    //       instead of failing a single fixed-size read that
                    //       spans into unmapped memory.  When a needed word
                    //       cannot be read, the associated pattern simply
                    //       does not match and resolution falls through to
                    //       the fallback (or error) handling below.
                    //
                    if (!ReadMoreInstructions(
                            process, current, instructions,
                            ref read, 1, ref error))
                    {
                        return false;
                    }

                    long offset;

                    if (TryDecode_B(instructions[0], out offset))
                    {
                        current = Add(current, offset);
                        continue;
                    }

                    int register1;
                    int register2;
                    ulong absolute;
                    IntPtr address2;

                    if (ReadMoreInstructions(
                            process, current, instructions,
                            ref read, 2, ref error) &&
                        TryDecode_Ldr_Literal_X(
                            instructions[0], out register1, out offset) &&
                        TryDecode_Br_X(instructions[1], out register2) &&
                        (register1 == register2))
                    {
                        address2 = Add(current, offset);

                        if (!ReadU64(
                                process, address2, out absolute,
                                ref error))
                        {
                            return false;
                        }

                        address = new IntPtr((long)absolute);
                        kind = PatchKind.AbsoluteAddress;

                        return true;
                    }

                    int value1;
                    IntPtr page;

                    if (ReadMoreInstructions(
                            process, current, instructions,
                            ref read, 3, ref error) &&
                        TryDecode_Adrp(
                            instructions[0], out register1, out offset) &&
                        TryDecode_Add_Imm12(
                            instructions[1], register1, register1,
                            out value1) &&
                        TryDecode_Br_X(instructions[2], out register2) &&
                        (register1 == register2))
                    {
                        page = PageOf(current);

                        address2 = Add(page, offset);
                        address2 = Add(address2, value1);

                        address = address2;
                        kind = PatchKind.AbsoluteAddress;

                        return true;
                    }

                    int value2;

                    if (ReadMoreInstructions(
                            process, current, instructions,
                            ref read, 4, ref error) &&
                        TryDecode_Adrp(
                            instructions[0], out register1, out offset) &&
                        TryDecode_Add_Imm12(
                            instructions[1], register1, register1,
                            out value1) &&
                        TryDecode_Ldr_X_Unsigned(
                            instructions[2], register1, register1,
                            out value2) &&
                        TryDecode_Br_X(instructions[3], out register2) &&
                        (register1 == register2))
                    {
                        page = PageOf(current);

                        address2 = Add(page, offset);
                        address2 = Add(address2, value1);
                        address2 = Add(address2, value2);

                        if (!ReadU64(
                                process, address2, out absolute,
                                ref error))
                        {
                            return false;
                        }

                        address = new IntPtr((long)absolute);
                        kind = PatchKind.AbsoluteAddress;

                        return true;
                    }

                    if (allowFallback)
                    {
                        address = current;
                        kind = PatchKind.AbsoluteAddress;

                        return true;
                    }
                    else
                    {
                        byte[] readBytes = new byte[read * sizeof(uint)];

                        for (int index = 0; index < read; index++)
                        {
                            Array.Copy(
                                BitConverter.GetBytes(instructions[index]),
                                0, readBytes, index * sizeof(uint),
                                sizeof(uint));
                        }

                        error = String.Format(
                            "hit unrecognized sequence at {0} " +
                            "({1}) for processor: {2}, {3}: {4}",
                            current, follow, _Arch.ARM64, kind,
                            Utility.ToHexadecimalString(readBytes));

                        return false;
                    }
                }

                error = String.Format(
                    "hit jump-stub follow limit {0} for " +
                    "processor: {1}, {2}", maximumFollow,
                    _Arch.ARM64, kind);

                return false;
            }
            #endregion
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Constants
#if WINDOWS
        //
        // NOTE: These are the memory protection flags to set just prior
        //       to patching in the trampoline.
        //
        /// <summary>
        /// The memory protection applied just prior to patching in the
        /// trampoline on Windows.
        /// </summary>
        private static readonly UNM.MemoryProtection disableProtection =
            UNM.MemoryProtection.PAGE_EXECUTE_READWRITE;
#endif

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the maximum number of jump-stubs to follow when
        //       trying to resolve the final code address to patch.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The default maximum number of jump-stubs to follow when resolving
        /// the final code address to patch.
        /// </summary>
        private static int defaultMaximumFollow = 80; // TODO: Good default?

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: These should be the list of possible names of (private)
        //       members within the DynamicMethod class that could return
        //       its RuntimeMethodHandle.
        //
        /// <summary>
        /// The candidate member names on the DynamicMethod class that can
        /// yield its runtime method handle.
        /// </summary>
        private static readonly string[] descriptorMemberNames =
        {
            "GetMethodDescriptor", // .NET (Core/5+) non-public method
            "m_method"             // .NET Framework non-public field
        };

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of the environment variable to set that
        //       can force the use of jump-stubs to be enabled or disabled.
        //
        /// <summary>
        /// The name of the environment variable that can force the use of
        /// jump-stubs on or off.
        /// </summary>
        private const string UseJumpStubsEnvVarName = "UseJumpStubs";

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This is the name of the environment variable to set that
        //       can forcibly allow (or deny) legacy patching semantics.
        //
        /// <summary>
        /// The name of the environment variable that can force legacy patching
        /// semantics to be allowed or denied.
        /// </summary>
        private const string AllowLegacyPatchEnvVarName = "AllowLegacyPatch";

        ///////////////////////////////////////////////////////////////////////

#if UNIX
        //
        // NOTE: This is the name of the file that contains memory page
        //       information, e.g. permissions, on Linux.
        //
        // HACK: This is purposely not read-only.
        //
        /// <summary>
        /// The name of the file containing memory page information (such as
        /// permissions) on Linux.
        /// </summary>
        private static string linuxMemoryPageMapFileName = "/proc/self/maps";
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Data
        //
        // NOTE: This field is used to synchronize access to the patches
        //       associated with each processor architecture (see below)
        //       and the cached field information for the method handle
        //       of dynamic methods.
        //
        /// <summary>
        /// The object used to synchronize access to the patch tables and the
        /// cached dynamic-method handle information.
        /// </summary>
        private static readonly object syncRoot = new object();

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This field stores the cached method information for the
        //       GetMethodDescriptor private method of the DynamicMethod
        //       class.
        //
        /// <summary>
        /// The cached member information used to obtain the runtime method
        /// handle of a dynamic method.
        /// </summary>
        private static MemberInfo descriptorMember;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This field contains a collection of byte arrays, one for
        //       each supported processor architecture, that is a sequence
        //       of machine instructions that act as a trampoline for the
        //       hooked managed method.
        //
        /// <summary>
        /// The per-architecture trampoline patch byte sequences.
        /// </summary>
        private static PatchDictionary patches = null;

        ///////////////////////////////////////////////////////////////////////

        //
        // NOTE: This field contains a collection of byte arrays, two for
        //       each supported processor architecture, that is a sequence
        //       of machine instructions that act as a "jump-stub".  The
        //       target portion of the stop value (i.e. the zeros) must be
        //       extracted and used as the offset [relative to the current
        //       instruction pointer] used to jump to the target method.
        //       The follow value indicates that an offset [also relative
        //       to the current instruction pointer] must be extracted and
        //       followed to the next instruction in the chain.
        //
        /// <summary>
        /// The per-architecture jump-stub byte sequences (target and follow
        /// forms).
        /// </summary>
        private static JumpStubDictionary jumpStubs = null;

        ///////////////////////////////////////////////////////////////////////

#if UNIX
        //
        // NOTE: This field is used to store the saved module handle that
        //       was used to obtain the "__clear_cache" function, et al,
        //       for the processor architecture(s) that require its use.
        //
        /// <summary>
        /// The saved module handle used to obtain the cache-clearing function
        /// on Unix.
        /// </summary>
        private static IntPtr clearCacheModule = IntPtr.Zero;
#endif
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Unsafe Native Methods Class
        /// <summary>
        /// Contains the platform native method declarations (Windows virtual
        /// memory and instruction-cache APIs, and the Unix mprotect,
        /// instruction-cache, and JIT write-protect APIs) used to read, patch,
        /// and flush executable memory.
        /// </summary>
        [SuppressUnmanagedCodeSecurity()]
        [ObjectId("7b59df73-bbfc-4854-98e2-98d610f2b8be")]
        internal static class UnsafeNativeMethods
        {
#if WINDOWS
            #region Windows Virtual Memory Constants
            /// <summary>
            /// The Windows memory protection constants used with the
            /// <c>VirtualProtect</c> native function.
            /// </summary>
            [Flags()]
            [ObjectId("149d70a3-460c-4a1e-a22a-b8553cec7213")]
            internal enum MemoryProtection : uint
            {
                /// <summary>
                /// No access protection.
                /// </summary>
                PAGE_NONE = 0x0000,
                /// <summary>
                /// No-access protection.
                /// </summary>
                PAGE_NOACCESS = 0x0001,
                /// <summary>
                /// Read-only protection.
                /// </summary>
                PAGE_READONLY = 0x0002,
                /// <summary>
                /// Read-write protection.
                /// </summary>
                PAGE_READWRITE = 0x0004,
                /// <summary>
                /// Write-copy protection.
                /// </summary>
                PAGE_WRITECOPY = 0x0008,
                /// <summary>
                /// Execute-only protection.
                /// </summary>
                PAGE_EXECUTE = 0x0010,
                /// <summary>
                /// Execute and read protection.
                /// </summary>
                PAGE_EXECUTE_READ = 0x0020,
                /// <summary>
                /// Execute, read, and write protection.
                /// </summary>
                PAGE_EXECUTE_READWRITE = 0x0040,
                /// <summary>
                /// Execute and write-copy protection.
                /// </summary>
                PAGE_EXECUTE_WRITECOPY = 0x0080,
                /// <summary>
                /// Guard-page modifier protection.
                /// </summary>
                PAGE_GUARD = 0x0100,
                /// <summary>
                /// No-cache modifier protection.
                /// </summary>
                PAGE_NOCACHE = 0x0200,
                /// <summary>
                /// Write-combine modifier protection.
                /// </summary>
                PAGE_WRITECOMBINE = 0x0400
            }
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Windows Virtual Memory Methods
            /// <summary>
            /// Changes the memory protection of a region (Windows
            /// <c>VirtualProtect</c>).
            /// </summary>
            /// <param name="address">
            /// The base address of the region.
            /// </param>
            /// <param name="size">
            /// The size of the region, in bytes.
            /// </param>
            /// <param name="newProtection">
            /// The new protection to apply.
            /// </param>
            /// <param name="oldProtection">
            /// Receives the previous protection.
            /// </param>
            /// <returns>
            /// Non-zero on success; otherwise, zero.
            /// </returns>
            [DllImport(DllName.Kernel32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool VirtualProtect(
                /* LPVOID */ IntPtr address,
                /* SIZE_T */ uint size,
                /* DWORD */ MemoryProtection newProtection,
                /* PDWORD */ out MemoryProtection oldProtection
            );

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Flushes the instruction cache for a region (Windows
            /// <c>FlushInstructionCache</c>).
            /// </summary>
            /// <param name="hProcess">
            /// A handle to the process.
            /// </param>
            /// <param name="baseAddress">
            /// The base address of the region.
            /// </param>
            /// <param name="size">
            /// The size of the region, in bytes.
            /// </param>
            /// <returns>
            /// Non-zero on success; otherwise, zero.
            /// </returns>
            [DllImport(DllName.Kernel32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool FlushInstructionCache(
                /* HANDLE */ IntPtr hProcess,
                /* LPCVOID */ IntPtr baseAddress,
                /* SIZE_T */ uint size
            );

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets a pseudo-handle for the current process (Windows
            /// <c>GetCurrentProcess</c>).
            /// </summary>
            /// <returns>
            /// A handle to the current process.
            /// </returns>
            [DllImport(DllName.Kernel32,
                CallingConvention = CallingConvention.Winapi)]
            internal static extern IntPtr GetCurrentProcess();

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Reads memory from a process (Windows <c>ReadProcessMemory</c>).
            /// </summary>
            /// <param name="hProcess">
            /// A handle to the process.
            /// </param>
            /// <param name="baseAddress">
            /// The address to read from.
            /// </param>
            /// <param name="buffer">
            /// The buffer that receives the bytes read.
            /// </param>
            /// <param name="size">
            /// The number of bytes to read.
            /// </param>
            /// <param name="read">
            /// Receives the number of bytes actually read.
            /// </param>
            /// <returns>
            /// Non-zero on success; otherwise, zero.
            /// </returns>
            [DllImport(DllName.Kernel32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool ReadProcessMemory(
                /* HANDLE */ IntPtr hProcess,
                /* LPCVOID */ IntPtr baseAddress,
                /* LPVOID */ byte[] buffer,
                /* SIZE_T */ uint size,
                /* PSIZE_T */ ref uint read
            );

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Writes memory to a process (Windows <c>WriteProcessMemory</c>).
            /// </summary>
            /// <param name="hProcess">
            /// A handle to the process.
            /// </param>
            /// <param name="baseAddress">
            /// The address to write to.
            /// </param>
            /// <param name="buffer">
            /// The bytes to write.
            /// </param>
            /// <param name="size">
            /// The number of bytes to write.
            /// </param>
            /// <param name="wrote">
            /// Receives the number of bytes actually written.
            /// </param>
            /// <returns>
            /// Non-zero on success; otherwise, zero.
            /// </returns>
            [DllImport(DllName.Kernel32,
                CallingConvention = CallingConvention.Winapi,
                SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool WriteProcessMemory(
                /* HANDLE */ IntPtr hProcess,
                /* LPVOID */ IntPtr baseAddress,
                /* LPCVOID */ byte[] buffer,
                /* SIZE_T */ uint size,
                /* PSIZE_T */ ref uint wrote
            );
            #endregion
#endif

            ///////////////////////////////////////////////////////////////////
#if UNIX
            #region Unix Virtual Memory Constants
            /// <summary>
            /// The no-access memory protection constant (Unix).
            /// </summary>
            internal const int PROT_NONE = 0x0;
            /// <summary>
            /// The read memory protection constant (Unix).
            /// </summary>
            internal const int PROT_READ = 0x1;
            /// <summary>
            /// The write memory protection constant (Unix).
            /// </summary>
            internal const int PROT_WRITE = 0x2;
            /// <summary>
            /// The execute memory protection constant (Unix).
            /// </summary>
            internal const int PROT_EXEC = 0x4;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The read and execute memory protection constant (Unix).
            /// </summary>
            internal const int PROT_RX = PROT_READ | PROT_EXEC;
            /// <summary>
            /// The read and write memory protection constant (Unix).
            /// </summary>
            internal const int PROT_RW = PROT_READ | PROT_WRITE;
            /// <summary>
            /// The read, write, and execute memory protection constant (Unix).
            /// </summary>
            internal const int PROT_RWX = PROT_READ | PROT_WRITE | PROT_EXEC;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The assumed memory page size, in bytes (Unix).
            /// </summary>
            internal const int PAGE_SIZE = 4096;

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// The candidate library file names that may export the
            /// <c>__clear_cache</c> function (Unix).
            /// </summary>
            internal static readonly string[] clearCacheFileNames = {
                "libc.so.6", "libgcc_s.so.1", "libc.so", "libgcc_s.so"
            };

            /// <summary>
            /// The name of the cache-clearing function exported by the C
            /// runtime (Unix).
            /// </summary>
            internal static readonly string clearCacheFunctionName =
                "__clear_cache";
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Unix Virtual Memory Delegates
            /// <summary>
            /// Represents the native <c>__clear_cache</c> function used to
            /// flush the instruction cache for a memory range on Unix.
            /// </summary>
            /// <param name="beg">
            /// The start address of the range to flush.
            /// </param>
            /// <param name="end">
            /// The end address of the range to flush.
            /// </param>
            [ObjectId("85466456-e4f9-4775-b945-67f8951626ce")]
            [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
            internal delegate void ClearCacheDelegate(
                /* LPVOID */ IntPtr beg,
                /* LPVOID */ IntPtr end
            );
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Unix Virtual Memory Methods
            /// <summary>
            /// Changes the memory protection of a region (Unix
            /// <c>mprotect</c>).
            /// </summary>
            /// <param name="addr">
            /// The base address of the region.
            /// </param>
            /// <param name="len">
            /// The length of the region, in bytes.
            /// </param>
            /// <param name="prot">
            /// The new protection to apply.
            /// </param>
            /// <returns>
            /// Zero on success; otherwise, a non-zero error code.
            /// </returns>
            [DllImport("libc",
                CallingConvention = CallingConvention.Cdecl,
                SetLastError = true)]
            internal static extern int mprotect(
                /* LPVOID */ IntPtr addr,
                /* SIZE_T */ UIntPtr len,
                /* INT */ int prot
            );

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Gets the system memory page size (Unix <c>getpagesize</c>).
            /// </summary>
            /// <returns>
            /// The page size, in bytes.
            /// </returns>
            [DllImport("libc",
                CallingConvention = CallingConvention.Cdecl,
                SetLastError = false)]
            internal static extern int getpagesize();

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Invalidates the instruction cache for a range using the
            /// internal (statically linked) entry point (macOS).
            /// </summary>
            /// <param name="start">
            /// The start address of the range.
            /// </param>
            /// <param name="len">
            /// The length of the range, in bytes.
            /// </param>
            [DllImport("__Internal",
                CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "sys_icache_invalidate",
                ExactSpelling = true)]
            internal static extern void internal_sys_icache_invalidate(
                /* LPVOID */ IntPtr start,
                /* SIZE_T */ UIntPtr len
            );

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Invalidates the instruction cache for a range using the system
            /// library entry point (macOS).
            /// </summary>
            /// <param name="start">
            /// The start address of the range.
            /// </param>
            /// <param name="len">
            /// The length of the range, in bytes.
            /// </param>
            [DllImport("libSystem.B.dylib",
                CallingConvention = CallingConvention.Cdecl,
                EntryPoint = "sys_icache_invalidate",
                ExactSpelling = true)]
            internal static extern void system_sys_icache_invalidate(
                /* LPVOID */ IntPtr start,
                /* SIZE_T */ UIntPtr len
            );

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Determines whether per-thread JIT write protection is supported
            /// (macOS).
            /// </summary>
            /// <returns>
            /// A non-zero value when supported.
            /// </returns>
            [DllImport("libSystem.B.dylib", ExactSpelling = true)]
            internal static extern int pthread_jit_write_protect_supported_np();

            ///////////////////////////////////////////////////////////////////

            /// <summary>
            /// Enables or disables per-thread JIT write protection (macOS).
            /// </summary>
            /// <param name="enabled">
            /// Non-zero to enable write protection; zero to disable it.
            /// </param>
            /// <returns>
            /// Zero on success; otherwise, a non-zero error code.
            /// </returns>
            [DllImport("libSystem.B.dylib",
                CallingConvention = CallingConvention.Cdecl)]
            internal static extern int pthread_jit_write_protect_np(
                /* INT */ int enabled
            );
            #endregion

            ///////////////////////////////////////////////////////////////////

            #region Bolt Helper Library Methods
            /// <summary>
            /// Writes a code patch atomically using the Bolt helper library
            /// (macOS).
            /// </summary>
            /// <param name="dst">
            /// The destination address.
            /// </param>
            /// <param name="src">
            /// The source address of the patch bytes.
            /// </param>
            /// <param name="len">
            /// The length of the patch, in bytes.
            /// </param>
            /// <returns>
            /// Zero on success; otherwise, a non-zero error code.
            /// </returns>
            [DllImport("libBolt.dylib",
                CallingConvention = CallingConvention.Cdecl,
                SetLastError = true)]
            internal static extern int write_code_patch(
                /* LPVOID */ IntPtr dst,
                /* LPVOID */ IntPtr src,
                /* SIZE_T */ UIntPtr len
            );
            #endregion
#endif
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Initialization Methods
        /// <summary>
        /// Populates the per-architecture trampoline patch table, creating it
        /// when necessary.
        /// </summary>
        /// <param name="patches">
        /// On input, the patch table to populate (created when null); on
        /// output, the populated table.
        /// </param>
        private static void InitializePatches(
            ref PatchDictionary patches /* in, out */
            )
        {
            //
            // TODO: New processor architectures should be added
            //       here, e.g. IA64, ARM, ARM64, PowerPC, RISC-V,
            //       etc.
            //
            if (patches == null)
                patches = new PatchDictionary();

            //
            // NOTE: x86 trampoline -- PUSH the 32-bit target address onto
            //       the stack, then RET to it (clobbers no registers).
            //
            patches[_Arch.Intel] = new PatchPair(/* offset */ 1, new byte[] {
                0x68,                   /* PUSH imm32 */
                0x00, 0x00, 0x00, 0x00, /* target     */
                0xC3                    /* RET         */
            });

            //
            // NOTE: x64 trampoline -- MOV the 64-bit target address into
            //       r11, then JMP through it.
            //
            patches[_Arch.AMD64] = new PatchPair(/* offset */ 2, new byte[] {
                0x49, 0xBB,             /* MOV r11, imm64 */
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, /* target */
                0x41, 0xFF, 0xE3        /* JMP r11        */
            });

            //
            // NOTE: ARM (Thumb-2) trampoline -- LDR the 32-bit target
            //       address directly into PC.
            //
            patches[_Arch.ARM] = new PatchPair(/* offset */ 4, new byte[] {
                0xDF, 0xF8, 0x00, 0xF0, /* LDR.W PC, [PC, #0] */
                0x00, 0x00, 0x00, 0x00  /* target             */
            });

            //
            // NOTE: ARM64 trampoline -- LDR the 64-bit target address into
            //       X16, then BR through it.
            //
            patches[_Arch.ARM64] = new PatchPair(/* offset */ 8, new byte[] {
                0x50, 0x00, 0x00, 0x58, /* LDR X16, [PC, #8] */
                0x00, 0x02, 0x1F, 0xD6, /* BR X16            */
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 /* target */
            });
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Populates the per-architecture jump-stub table, creating it when
        /// necessary.
        /// </summary>
        /// <param name="jumpStubs">
        /// On input, the jump-stub table to populate (created when null); on
        /// output, the populated table.
        /// </param>
        private static void InitializeJumpStubs(
            ref JumpStubDictionary jumpStubs /* in, out */
            )
        {
            //
            // TODO: New processor architectures should be added
            //       here, e.g. IA64, ARM, ARM64, PowerPC, RISC-V,
            //       etc.
            //
            if (jumpStubs == null)
                jumpStubs = new JumpStubDictionary();

            //
            // NOTE: x64 jump stub using an absolute 64-bit address -- MOV
            //       the target into rax, then JMP through it.
            //
            jumpStubs[new JumpKey(_Arch.AMD64,
                PatchKind.AbsoluteAddress)] = new JumpPair(
                new PatchPair(/* offset */ 2, new byte[] {
                    0x48, 0xB8, /* MOV rax, imm64 */
                    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                    0xFF, 0xE0  /* JMP rax */
                })
            );

            //
            // NOTE: x64 jump stub using a relative 32-bit address.  The
            //       first form is JMP [rip + rel32]; the second is the
            //       shorter JMP rel32.
            //
            jumpStubs[new JumpKey(_Arch.AMD64,
                PatchKind.RelativeAddress)] = new JumpPair(
                new PatchPair(/* offset */ 2, new byte[] {
                    0xFF, 0x25, /* JMP [rip + rel32] */
                    0x00, 0x00, 0x00, 0x00 /* rel32 */
                }),
                new PatchPair(/* offset */ 1, new byte[] {
                    0xE9, /* JMP rel32 */
                    0x00, 0x00, 0x00, 0x00 /* rel32 */
                })
            );
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves and caches the DynamicMethod member used to obtain a
        /// dynamic method's runtime method handle.
        /// </summary>
        /// <param name="type">
        /// The DynamicMethod type to search.
        /// </param>
        /// <param name="descriptorMember">
        /// On output, receives the resolved member.
        /// </param>
        private static void InitializeDescriptorMember(
            Type type,                      /* in */
            ref MemberInfo descriptorMember /* out */
            )
        {
            if ((type == null) || (descriptorMemberNames == null))
                return;

            BindingFlags bindingFlags = BindingFlags.Instance |
                BindingFlags.NonPublic | BindingFlags.Public;

            Type returnType = typeof(RuntimeMethodHandle);

            foreach (string memberName in descriptorMemberNames)
            {
                MethodInfo methodInfo = type.GetMethod(
                    memberName, bindingFlags);

                if ((methodInfo != null) &&
                    (methodInfo.ReturnType == returnType))
                {
                    ParameterInfo[] parameterInfos =
                        methodInfo.GetParameters();

                    if ((parameterInfos != null) &&
                        (parameterInfos.Length == 0))
                    {
                        descriptorMember = methodInfo;
                        break;
                    }
                }

                FieldInfo fieldInfo = type.GetField(
                    memberName, bindingFlags);

                if ((fieldInfo != null) &&
                    (fieldInfo.FieldType == returnType))
                {
                    descriptorMember = fieldInfo;
                    break;
                }
            }
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Methods
        /// <summary>
        /// Gets a handle to the current process for the platform memory
        /// routines.
        /// </summary>
        /// <returns>
        /// A handle to the current process.
        /// </returns>
        private static IntPtr GetCurrentProcess()
        {
#if WINDOWS
            if (Utility.IsWindowsOperatingSystem())
                return UNM.GetCurrentProcess();
#endif

            return IntPtr.Zero;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the bytes of a native pointer address.
        /// </summary>
        /// <param name="address">
        /// The address to convert.
        /// </param>
        /// <returns>
        /// The address bytes.
        /// </returns>
        private static byte[] GetAddressBytes(
            IntPtr address /* in */
            )
        {
            return (IntPtr.Size == sizeof(ulong)) ?
                BitConverter.GetBytes(address.ToInt64()) :
                BitConverter.GetBytes(address.ToInt32());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the end address of a region given its start address and
        /// length.
        /// </summary>
        /// <param name="address">
        /// The start address.
        /// </param>
        /// <param name="length">
        /// The length, in bytes.
        /// </param>
        /// <returns>
        /// The end address.
        /// </returns>
        private static IntPtr GetEndAddress(
            IntPtr address, /* in */
            UIntPtr length  /* in */
            )
        {
            return new IntPtr(
                address.ToInt64() + (long)length.ToUInt64());
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the page-aligned start and end addresses spanning the
        /// given region.
        /// </summary>
        /// <param name="address">
        /// The region start address.
        /// </param>
        /// <param name="length">
        /// The length, in bytes.
        /// </param>
        /// <param name="pageSize">
        /// The page size, in bytes.
        /// </param>
        /// <param name="pageStart">
        /// On output, receives the page-aligned start address.
        /// </param>
        /// <param name="pageEnd">
        /// On output, receives the page-aligned end address.
        /// </param>
        private static void GetPageAddressRange(
            IntPtr address,     /* in */
            int length,         /* in */
            int pageSize,       /* in */
            ref long pageStart, /* out */
            ref long pageEnd    /* out */
            )
        {
            long startAddress = address.ToInt64();
            long endAddress = startAddress + length;
            long pageMask = ~((long)pageSize - 1);

            pageStart = startAddress & pageMask;
            pageEnd = (endAddress + pageSize - 1) & pageMask;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the runtime method handle for a method, resolving dynamic
        /// methods through the cached descriptor member.
        /// </summary>
        /// <param name="method">
        /// The method whose handle is requested.
        /// </param>
        /// <param name="handle">
        /// On output, receives the runtime method handle.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool GetRuntimeMethodHandle(
            MethodBase method,             /* in */
            out RuntimeMethodHandle handle /* out */
            )
        {
            handle = default(RuntimeMethodHandle);

            if (method == null)
                return false;

            DynamicMethod dynamicMethod = method as DynamicMethod;

            if (dynamicMethod != null)
            {
                lock (syncRoot) /* TRANSACTIONAL */
                {
                    MethodInfo methodInfo = descriptorMember as MethodInfo;

                    if (methodInfo != null)
                    {
                        handle = (RuntimeMethodHandle)methodInfo.Invoke(
                            dynamicMethod, null);

                        return true;
                    }

                    FieldInfo fieldInfo = descriptorMember as FieldInfo;

                    if (fieldInfo != null)
                    {
                        handle = (RuntimeMethodHandle)fieldInfo.GetValue(
                            dynamicMethod);

                        return true;
                    }
                }

                handle = dynamicMethod.MethodHandle;
                return true;
            }

            handle = method.MethodHandle;
            return true;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the trampoline patch template for the given processor
        /// architecture.
        /// </summary>
        /// <param name="processor">
        /// The processor architecture.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The patch template, or a default value on failure.
        /// </returns>
        private static PatchPair GetPatch(
            _Arch processor, /* in */
            ref Result error /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (patches == null)
                {
                    error = "patches unavailable";
                    return null;
                }

                PatchPair anyPair;

                if (!patches.TryGetValue(processor, out anyPair))
                {
                    error = String.Format(
                        "no patch for processor: {0}", processor);

                    return null;
                }

                return anyPair;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Creates the trampoline patch bytes targeting the given address for
        /// the given processor architecture.
        /// </summary>
        /// <param name="processor">
        /// The processor architecture.
        /// </param>
        /// <param name="address">
        /// The target address to encode into the patch.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The patch bytes, or null on failure.
        /// </returns>
        private static byte[] CreatePatch(
            _Arch processor, /* in */
            IntPtr address,  /* in */
            ref Result error /* out */
            )
        {
            byte[] addressBytes = GetAddressBytes(address);

            if (addressBytes == null)
            {
                error = "invalid address bytes";
                return null;
            }

            PatchPair patch = GetPatch(processor, ref error);

            if (patch == null)
            {
                error = String.Format(
                    "invalid patch for processor: {0}", processor);

                return null;
            }

            byte[] oldPatch = patch.Y;

            if (oldPatch == null)
            {
                error = String.Format(
                    "bad patch for processor: {0}", processor);

                return null;
            }

            int patchLength = oldPatch.Length;
            int addressLength = addressBytes.Length;
            int offset = patch.X;

            if ((offset < 0) ||
                ((offset + addressLength) > patchLength))
            {
                error = String.Format(
                    "invalid patch offset for processor: {0}",
                    processor);

                return null;
            }

            byte[] newPatch = new byte[patchLength];

            Array.Copy(oldPatch, newPatch, patchLength);

            Array.Copy(
                addressBytes, 0, newPatch, offset, addressLength);

            return newPatch;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the jump-stub template for the given processor architecture
        /// and patch kind.
        /// </summary>
        /// <param name="processor">
        /// The processor architecture.
        /// </param>
        /// <param name="kind">
        /// The patch kind.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// The jump-stub template, or a default value on failure.
        /// </returns>
        private static JumpPair GetJumpStub(
            _Arch processor, /* in */
            PatchKind kind,  /* in */
            ref Result error /* out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (jumpStubs == null)
                {
                    error = "jump-stubs unavailable";
                    return null;
                }

                JumpPair anyPair;

                if (!jumpStubs.TryGetValue(
                        new JumpKey(processor, kind), out anyPair))
                {
                    error = String.Format(
                        "no jump-stub for processor: {0}", processor);

                    return null;
                }

                return anyPair;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the maximum of the supplied nullable values, ignoring nulls.
        /// </summary>
        /// <param name="values">
        /// The values to consider.
        /// </param>
        /// <returns>
        /// The maximum value, or null when none are supplied.
        /// </returns>
        private static int? GetMaximumValue(
            params int[] values /* in */
            )
        {
            int? maximum = null;

            if (values != null)
            {
                foreach (int value in values)
                {
                    if (maximum == null)
                        maximum = value;
                    else if (value > (int)maximum)
                        maximum = value;
                }
            }

            return maximum;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether jump-stubs should be used for the given
        /// processor architecture, honoring the controlling environment
        /// variable.
        /// </summary>
        /// <param name="processor">
        /// The processor architecture.
        /// </param>
        /// <returns>
        /// Non-zero when jump-stubs should be used; otherwise, zero.
        /// </returns>
        private static bool ShouldTryToUseJumpStubs(
            _Arch processor /* in */
            )
        {
            string value = Utility.GetEnvironmentVariable(
                UseJumpStubsEnvVarName);

            if (value != null)
            {
                bool boolValue = false;
                Result error = null;

                if (Value.GetBoolean2(
                        value, ValueFlags.AnyBoolean, null,
                        ref boolValue, ref error) == ReturnCode.Ok)
                {
                    return boolValue;
                }
                else
                {
                    Utility.DebugTrace(String.Format(
                        "ShouldTryToUseJumpStubs({0}) error: {1}",
                        processor, error), typeof(HookOps).Name,
                        TracePriority.Highest);
                }
            }

            if (Utility.IsDotNetCore5xOrHigher()) /* TESTED BOTH */
            {
                switch (processor)
                {
                    case _Arch.AMD64:
                    case _Arch.ARM64:
                        {
                            return true;
                        }
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether legacy patching should be allowed for the given
        /// processor architecture, honoring the controlling environment
        /// variable.
        /// </summary>
        /// <param name="processor">
        /// The processor architecture.
        /// </param>
        /// <returns>
        /// Non-zero when legacy patching is allowed; otherwise, zero.
        /// </returns>
        private static bool ShouldAllowLegacyPatch(
            _Arch processor /* in */
            )
        {
            string value = Utility.GetEnvironmentVariable(
                AllowLegacyPatchEnvVarName);

            if (value != null)
            {
                bool boolValue = false;
                Result error = null;

                if (Value.GetBoolean2(
                        value, ValueFlags.AnyBoolean, null,
                        ref boolValue, ref error) == ReturnCode.Ok)
                {
                    return boolValue;
                }
                else
                {
                    Utility.DebugTrace(String.Format(
                        "ShouldAllowLegacyPatch({0}) error: {1}",
                        processor, error), typeof(HookOps).Name,
                        TracePriority.Highest);
                }
            }

            if (!Utility.IsDotNetCore7xOrHigher()) /* TESTED BOTH */
            {
                switch (processor)
                {
                    case _Arch.AMD64:
                    case _Arch.ARM64:
                        {
                            return true;
                        }
                }
            }

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the effective allow-legacy and allow-fallback decisions
        /// from the supplied patch flags and the architecture defaults.
        /// </summary>
        /// <param name="processor">
        /// The processor architecture.
        /// </param>
        /// <param name="patchFlags">
        /// The requested patch flags, if any.
        /// </param>
        /// <param name="allowLegacy">
        /// On output, whether legacy patching is allowed.
        /// </param>
        /// <param name="allowFallback">
        /// On output, whether fallback is allowed.
        /// </param>
        private static void GetPatchFlags(
            _Arch processor,        /* in */
            PatchFlags? patchFlags, /* in: OPTIONAL */
            out bool allowLegacy,   /* out */
            out bool allowFallback  /* out */
            )
        {
            if (patchFlags != null)
            {
                allowLegacy = HasPatchFlags((PatchFlags)patchFlags,
                    PatchFlags.AllowLegacy, true);

                allowFallback = HasPatchFlags((PatchFlags)patchFlags,
                    PatchFlags.AllowFallback, true);
            }
            else
            {
                allowLegacy = ShouldAllowLegacyPatch(processor);

                //
                // HACK: The fallback path for ARM64 on macOS can cause
                //       crashes (e.g. "bus error"); therefore, skip it
                //       by default in that case.
                //
                if ((processor != _Arch.ARM64) ||
                    !Utility.IsMacintoshOperatingSystem())
                {
                    allowFallback = true;
                }
                else
                {
                    allowFallback = false;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the supplied patch flags contain the given
        /// flags.
        /// </summary>
        /// <param name="flags">
        /// The flags to test.
        /// </param>
        /// <param name="hasFlags">
        /// The flags to look for.
        /// </param>
        /// <param name="all">
        /// Non-zero to require all of the flags; zero to require any.
        /// </param>
        /// <returns>
        /// Non-zero when the flags are present; otherwise, zero.
        /// </returns>
        private static bool HasPatchFlags(
            PatchFlags flags,    /* in */
            PatchFlags hasFlags, /* in */
            bool all             /* in */
            )
        {
            if (all)
                return ((flags & hasFlags) == hasFlags);
            else
                return ((flags & hasFlags) != PatchFlags.None);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Attempts to resolve the final target address by following a
        /// jump-stub sequence for the given processor architecture, using the
        /// architecture-specific decoder.
        /// </summary>
        /// <param name="process">
        /// A handle to the process whose memory is accessed.
        /// </param>
        /// <param name="original">
        /// The original (starting) address.
        /// </param>
        /// <param name="processor">
        /// The processor architecture.
        /// </param>
        /// <param name="kind">
        /// The patch kind.
        /// </param>
        /// <param name="maximumFollow">
        /// The maximum number of jump-stubs to follow.
        /// </param>
        /// <param name="jumpStub">
        /// The jump-stub template to match.
        /// </param>
        /// <param name="resolved">
        /// On output, receives the resolved target address.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool TryToResolveWithJumpStub(
            IntPtr process,      /* in */
            IntPtr original,     /* in */
            _Arch processor,     /* in */
            PatchKind kind,      /* in */
            int maximumFollow,   /* in */
            JumpPair jumpStub,   /* in */
            ref IntPtr resolved, /* out */
            ref Result error     /* out */
            )
        {
            if (jumpStub == null)
            {
                error = "invalid jump-stub";
                return false;
            }

            PatchPair stopPair = jumpStub.X;

            if (stopPair == null)
            {
                error = "invalid stop pair for jump-stub";
                return false;
            }

            PatchPair followPair = jumpStub.Y;

            if ((followPair == null) &&
                (kind == PatchKind.RelativeAddress))
            {
                error = "invalid follow pair for jump-stub";
                return false;
            }

            byte[] stopOnMatch = stopPair.Y;

            if (stopOnMatch == null)
            {
                error = "invalid stop-on-match for jump-stub";
                return false;
            }

            byte[] followOnMatch = (followPair != null) ?
                followPair.Y : null;

            if ((followOnMatch == null) &&
                (kind == PatchKind.RelativeAddress))
            {
                error = "invalid follow-on-match for jump-stub";
                return false;
            }

            int stopLength = stopOnMatch.Length;

            if (stopLength == 0)
            {
                error = "invalid stop-on-match length for jump-stub";
                return false;
            }

            int followLength = (followOnMatch != null) ?
                followOnMatch.Length : Length.Invalid;

            if (followLength == 0)
            {
                error = "invalid follow-on-match length for jump-stub";
                return false;
            }

            int stopSize = (kind == PatchKind.AbsoluteAddress) ?
                IntPtr.Size : sizeof(int);

            int stopOffset = stopPair.X;

            if ((stopOffset <= 0) ||
                ((stopOffset + stopSize) > stopLength))
            {
                error = "invalid stop-on-match offset for jump-stub";
                return false;
            }

            int followOffset = (followPair != null) ?
                followPair.X : Index.Invalid;

            if ((followOnMatch != null) && ((followOffset <= 0) ||
                ((followOffset + sizeof(int)) > followLength)))
            {
                error = "invalid follow-on-match offset for jump-stub";
                return false;
            }

            int? bufferLength = GetMaximumValue(stopLength, followLength);

            if (bufferLength == null)
            {
                error = "invalid buffer length for jump-stub";
                return false;
            }

            byte[] buffer = new byte[(int)bufferLength];
            IntPtr current = original;

            for (int follow = 0; follow < maximumFollow; follow++)
            {
                Array.Clear(buffer, 0, (int)bufferLength); /* REDUNDANT */

                if (Read(process,
                        current, buffer, ref error) != ReturnCode.Ok)
                {
                    return false;
                }

                if ((stopOnMatch != null) && Utility.ArrayEquals(
                        buffer, stopOnMatch, stopOffset))
                {
                    if (kind == PatchKind.RelativeAddress)
                    {
                        if (IntPtr.Size == sizeof(long))
                        {
                            resolved = new IntPtr(
                                current.ToInt64() + stopLength +
                                BitConverter.ToInt32(buffer, stopOffset));
                        }
                        else
                        {
                            resolved = new IntPtr(
                                current.ToInt32() + stopLength +
                                BitConverter.ToInt32(buffer, stopOffset));
                        }

                        return true;
                    }
                    else if (kind == PatchKind.AbsoluteAddress)
                    {
                        int nextOffset = stopLength -
                            (stopOffset + IntPtr.Size);

                        if ((nextOffset >= 0) && Utility.ArrayEquals(
                                buffer, stopOnMatch, stopLength -
                                nextOffset, nextOffset))
                        {
                            if (IntPtr.Size == sizeof(long))
                            {
                                resolved = new IntPtr(
                                    current.ToInt64() + stopOffset);
                            }
                            else
                            {
                                resolved = new IntPtr(
                                    current.ToInt32() + stopOffset);
                            }

                            return true;
                        }
                    }
                    else
                    {
                        error = String.Format(
                            "unsupported stop-on-match patch kind {0}",
                            kind);

                        return false;
                    }
                }

                if ((followOnMatch != null) && Utility.ArrayEquals(
                        buffer, followOnMatch, followOffset))
                {
                    if (IntPtr.Size == sizeof(long))
                    {
                        current = new IntPtr(
                            current.ToInt64() + followLength +
                            BitConverter.ToInt32(buffer, followOffset));
                    }
                    else
                    {
                        current = new IntPtr(
                            current.ToInt32() + followLength +
                            BitConverter.ToInt32(buffer, followOffset));
                    }

                    continue;
                }

                error = String.Format(
                    "hit unrecognized sequence at {0} " +
                    "({1}) for processor: {2}, {3}: {4}",
                    current, follow, processor, kind,
                    Utility.ToHexadecimalString(buffer));

                return false;
            }

            error = String.Format(
                "hit jump-stub follow limit {0} for " +
                "processor: {1}, {2}", maximumFollow,
                processor, kind);

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the jump-stub for the architecture and attempts to resolve the
        /// final target address from the given address.
        /// </summary>
        /// <param name="process">
        /// A handle to the process whose memory is accessed.
        /// </param>
        /// <param name="processor">
        /// The processor architecture.
        /// </param>
        /// <param name="maximumFollow">
        /// The maximum number of jump-stubs to follow.
        /// </param>
        /// <param name="address">
        /// On input, the starting address; on success, the resolved address.
        /// </param>
        /// <param name="kind">
        /// On output, receives the resolved patch kind.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool GetAndTryToResolveWithJumpStub(
            IntPtr process,     /* in */
            _Arch processor,    /* in */
            int maximumFollow,  /* in */
            ref IntPtr address, /* in, out */
            ref PatchKind kind, /* out */
            ref Result error    /* out */
            )
        {
            JumpPair jumpStub = null;
            ResultList errors = null;

            foreach (PatchKind localKind in new PatchKind[] {
                    PatchKind.AbsoluteAddress, /* 64-bit (?) */
                    PatchKind.RelativeAddress  /* 32-bit (?) */
                })
            {
                Result localError = null; /* REUSED */

                jumpStub = GetJumpStub(
                    processor, localKind, ref localError);

                if (jumpStub == null)
                {
                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    continue;
                }

                localError = null;

                if (!TryToResolveWithJumpStub(
                        process, address, processor, localKind,
                        maximumFollow, jumpStub, ref address,
                        ref localError))
                {
                    if (localError != null)
                    {
                        if (errors == null)
                            errors = new ResultList();

                        errors.Add(localError);
                    }

                    continue;
                }

                kind = localKind;
                return true;
            }

            error = errors;
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

#if WINDOWS
        /// <summary>
        /// Reads bytes from the target memory using the Windows process memory
        /// API.
        /// </summary>
        /// <param name="process">
        /// A handle to the process whose memory is accessed.
        /// </param>
        /// <param name="address">
        /// The target memory address.
        /// </param>
        /// <param name="patch">
        /// The buffer that receives the bytes read.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode WindowsRead(
            IntPtr process,  /* in */
            IntPtr address,  /* in */
            byte[] patch,    /* in, out */
            ref Result error /* out */
            )
        {
            if (patch == null)
            {
                error = "invalid patch";
                return ReturnCode.Error;
            }

            uint length = (uint)patch.Length;

            if (length == 0)
            {
                error = "invalid patch length";
                return ReturnCode.Error;
            }

            try
            {
                uint read = 0;

                if (!UNM.ReadProcessMemory(
                        process, address, patch,
                        length, ref read))
                {
                    error = Utility.GetErrorMessage();
                    return ReturnCode.Error;
                }

                if (read != length)
                {
                    error = String.Format(
                        "ReadProcessMemory: read {0}, wanted {1}",
                        read, length);

                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes bytes to the target memory using the Windows process memory
        /// API, adjusting page protection and flushing the instruction cache
        /// as needed.
        /// </summary>
        /// <param name="process">
        /// A handle to the process whose memory is accessed.
        /// </param>
        /// <param name="address">
        /// The target memory address.
        /// </param>
        /// <param name="patch">
        /// The bytes to write.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode WindowsWrite(
            IntPtr process,  /* in */
            IntPtr address,  /* in */
            byte[] patch,    /* in */
            ref Result error /* out */
            )
        {
            if (patch == null)
            {
                error = "invalid patch";
                return ReturnCode.Error;
            }

            uint length = (uint)patch.Length;

            if (length == 0)
            {
                error = "invalid patch length";
                return ReturnCode.Error;
            }

            UNM.MemoryProtection oldProtection =
                UNM.MemoryProtection.PAGE_NONE;

            try
            {
                if (!UNM.VirtualProtect(
                        address, length, disableProtection,
                        out oldProtection))
                {
                    error = Utility.GetErrorMessage();
                    return ReturnCode.Error;
                }

                uint wrote = 0;

                if (!UNM.WriteProcessMemory(
                        process, address, patch, length,
                        ref wrote))
                {
                    error = Utility.GetErrorMessage();
                    return ReturnCode.Error;
                }

                if (wrote != length)
                {
                    error = String.Format(
                        "WriteProcessMemory: wrote {0}, wanted {1}",
                        wrote, length);

                    return ReturnCode.Error;
                }

                if (!UNM.FlushInstructionCache(
                        process, address, wrote))
                {
                    error = Utility.GetErrorMessage();
                    return ReturnCode.Error;
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
            finally
            {
                if (oldProtection != UNM.MemoryProtection.PAGE_NONE)
                {
                    UNM.MemoryProtection previousProtection;

                    if (!UNM.VirtualProtect(
                            address, length, oldProtection,
                            out previousProtection))
                    {
                        Utility.Complain(
                            null, ReturnCode.Error,
                            Utility.GetErrorMessage());
                    }

                    if (previousProtection != disableProtection)
                    {
                        Utility.DebugTrace(String.Format(
                            "VirtualProtect: previous {0}, expected {1}",
                            previousProtection, disableProtection),
                            typeof(HookOps).Name,
                            TracePriority.Highest);
                    }
                }
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

#if UNIX
        /// <summary>
        /// Gets the memory page size on Unix.
        /// </summary>
        /// <returns>
        /// The page size, in bytes.
        /// </returns>
        private static int UnixPageSize()
        {
            try
            {
                int pageSize = UNM.getpagesize();

                if (pageSize <= 0)
                    pageSize = UNM.PAGE_SIZE;

                return pageSize;
            }
            catch
            {
                return UNM.PAGE_SIZE;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Frees the cache-clearing module, complaining on failure rather than
        /// throwing.
        /// </summary>
        /// <param name="module">
        /// The module handle to free.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool FreeLibraryOrComplain(
            ref IntPtr module /* in, out */
            )
        {
            int lastError;

            if (Utility.FreeLibrary(module, out lastError))
            {
                module = IntPtr.Zero;
                return true;
            }
            else
            {
                Utility.Complain(
                    null, ReturnCode.Error, String.Format(
                    "FreeLibraryOrComplain({0}) error: {1}",
                    module, lastError));

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Determines whether the cache-clearing module handle is currently
        /// saved.
        /// </summary>
        /// <returns>
        /// Non-zero when a module handle is saved; otherwise, zero.
        /// </returns>
        private static bool HaveClearCacheModule()
        {
            lock (syncRoot)
            {
                return clearCacheModule != IntPtr.Zero;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the saved cache-clearing module handle.
        /// </summary>
        /// <returns>
        /// The saved module handle.
        /// </returns>
        private static IntPtr GetClearCacheModule()
        {
            lock (syncRoot)
            {
                return clearCacheModule;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Clears the saved cache-clearing module handle.
        /// </summary>
        /// <returns>
        /// Non-zero when a handle was cleared; otherwise, zero.
        /// </returns>
        private static bool ResetClearCacheModule()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (clearCacheModule != IntPtr.Zero)
                {
                    clearCacheModule = IntPtr.Zero;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Saves the cache-clearing module handle when one is not already
        /// saved.
        /// </summary>
        /// <param name="module">
        /// The module handle to save.
        /// </param>
        /// <returns>
        /// Non-zero when the handle was saved; otherwise, zero.
        /// </returns>
        private static bool MaybeSaveClearCacheModule(
            ref IntPtr module /* in, out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((module != IntPtr.Zero) &&
                    (clearCacheModule == IntPtr.Zero))
                {
                    clearCacheModule = module;
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Frees the saved cache-clearing module handle when one is present.
        /// </summary>
        /// <param name="module">
        /// On input, the module handle to free; cleared on output.
        /// </param>
        /// <returns>
        /// Non-zero when a handle was freed; otherwise, zero.
        /// </returns>
        private static bool MaybeFreeClearCacheModule(
            ref IntPtr module /* in, out */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if ((module != IntPtr.Zero) &&
                    (module != clearCacheModule))
                {
                    return FreeLibraryOrComplain(ref module);
                }
                else
                {
                    return false;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Resolves the native cache-clearing delegate from the candidate
        /// runtime libraries, saving the owning module.
        /// </summary>
        /// <returns>
        /// The resolved delegate, or null when none is available.
        /// </returns>
        private static UNM.ClearCacheDelegate UnixResolveClearCache()
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                string[] fileNames = UNM.clearCacheFileNames;

                if (fileNames == null)
                    return null;

                IntPtr module = IntPtr.Zero;
                Type delegateType = typeof(UNM.ClearCacheDelegate);

                try
                {
                    foreach (string fileName in fileNames)
                    {
                        int lastError; /* NOT USED */

                        module = GetClearCacheModule();

                        if (module == IntPtr.Zero)
                        {
                            module = Utility.LoadLibrary(
                                fileName, out lastError);

                            if (module == IntPtr.Zero)
                                continue;
                        }

                        IntPtr address = Utility.GetProcAddress(
                            module, UNM.clearCacheFunctionName,
                            out lastError);

                        if (address == IntPtr.Zero)
                        {
                            if (!FreeLibraryOrComplain(ref module))
                                module = IntPtr.Zero;

                            /* IGNORED */
                            ResetClearCacheModule();

                            continue;
                        }

                        if (!HaveClearCacheModule() &&
                            !MaybeSaveClearCacheModule(ref module))
                        {
                            continue;
                        }

                        Debug.Assert(module == GetClearCacheModule());

                        return Marshal.GetDelegateForFunctionPointer(
                            address, delegateType) as UNM.ClearCacheDelegate;
                    }
                }
                catch (Exception e)
                {
                    Utility.DebugTrace(
                        e, typeof(HookOps).Name,
                        TracePriority.Medium |
                            TracePriority.FromPlugin);
                }
                finally
                {
                    MaybeFreeClearCacheModule(ref module);
                }

                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads bytes from the target memory using the Unix memory API.
        /// </summary>
        /// <param name="process">
        /// A handle to the process whose memory is accessed.
        /// </param>
        /// <param name="address">
        /// The target memory address.
        /// </param>
        /// <param name="patch">
        /// The buffer that receives the bytes read.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode UnixRead(
            IntPtr process,  /* in */
            IntPtr address,  /* in */
            byte[] patch,    /* in, out */
            ref Result error /* out */
            )
        {
            if (address == IntPtr.Zero)
            {
                error = "invalid read address";
                return ReturnCode.Error;
            }

            if (patch == null)
            {
                error = "invalid patch";
                return ReturnCode.Error;
            }

            int length = patch.Length;

            if (length <= 0) /* IMPOSSIBLE (?) */
            {
                error = "invalid patch length";
                return ReturnCode.Error;
            }

            try
            {
                Marshal.Copy(address, patch, 0, length);
                return ReturnCode.Ok;
            }
            catch (Exception ex)
            {
                error = ex;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses a native pointer value from its textual (hexadecimal)
        /// representation.
        /// </summary>
        /// <param name="text">
        /// The text to parse.
        /// </param>
        /// <param name="value">
        /// On output, receives the parsed pointer.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode GetIntPtr(
            string text,     /* in */
            out IntPtr value /* out */
            )
        {
            if (IntPtr.Size == sizeof(long))
            {
                long longValue;

                if (long.TryParse(
                        text, NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out longValue))
                {
                    value = new IntPtr(longValue);
                    return ReturnCode.Ok;
                }
            }
            else
            {
                int intValue;

                if (int.TryParse(
                        text, NumberStyles.HexNumber,
                        CultureInfo.InvariantCulture,
                        out intValue))
                {
                    value = new IntPtr(intValue);
                    return ReturnCode.Ok;
                }
            }

            value = IntPtr.Zero;
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Lazily reads the lines of a file.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to read.
        /// </param>
        /// <returns>
        /// An enumerable over the lines of the file.
        /// </returns>
        private static IEnumerable<string> ReadLines(
            string fileName /* in */
            )
        {
#if NET_40
            return File.ReadLines(fileName);
#else
            //
            // TODO: Verify this compatibility shim works, i.e.
            //       when running on the .NET Framework 2.0.
            //
            string text = File.ReadAllText(fileName);

            if (text == null)
                return null;

            return text.Split(Characters.NewLine);
#endif
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Parses the memory page ranges (and their protections) for the
        /// current process from the Linux page-map file.
        /// </summary>
        /// <param name="fileName">
        /// The page-map file to parse.
        /// </param>
        /// <param name="pageRanges">
        /// On output, receives the parsed page ranges.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode GetPageRanges(
            string fileName,                       /* in */
            ref IEnumerable<PageRange> pageRanges, /* out */
            ref Result error                       /* out */
            )
        {
            try
            {
                IEnumerable<string> lines = ReadLines(fileName);

                if (lines != null)
                {
                    PageRangeList localPageRanges = new PageRangeList();
                    PageRange pageRange = new PageRange();

                    int[] indexes = {
                        Index.Invalid, Index.Invalid, Index.Invalid
                    };

                    foreach (string line in lines)
                    {
                        if (String.IsNullOrEmpty(line))
                            continue;

                        indexes[0] = line.IndexOf(
                            Characters.MinusSign);

                        if (indexes[0] == Index.Invalid)
                            continue;

                        indexes[2] = indexes[0] + 1; /* skip minus */

                        indexes[1] = line.IndexOf(
                            Characters.Space, indexes[2]);

                        if (indexes[1] == Index.Invalid)
                            continue;

                        if (GetIntPtr(
                                line.Substring(0, indexes[0]),
                                out pageRange.startAddress) != ReturnCode.Ok)
                        {
                            continue;
                        }

                        if (GetIntPtr(line.Substring(
                                indexes[2], indexes[1] - indexes[2]),
                                out pageRange.endAddress) != ReturnCode.Ok)
                        {
                            continue;
                        }

                        indexes[0] = indexes[1] + 1; /* skip space */

                        indexes[1] = line.IndexOf(
                            Characters.Space, indexes[0]);

                        if (indexes[1] == Index.Invalid)
                            indexes[1] = line.Length;

                        indexes[2] = indexes[1] - indexes[0];

                        string permissions = line.Substring(
                            indexes[0], Math.Min(3, indexes[2]));

                        pageRange.protection = UNM.PROT_NONE;

                        if ((permissions.Length >= 1) &&
                            (permissions[0] == Characters.r))
                        {
                            pageRange.protection |= UNM.PROT_READ;
                        }

                        if ((permissions.Length >= 2) &&
                            (permissions[1] == Characters.w))
                        {
                            pageRange.protection |= UNM.PROT_WRITE;
                        }

                        if ((permissions.Length >= 3) &&
                            (permissions[2] == Characters.x))
                        {
                            pageRange.protection |= UNM.PROT_EXEC;
                        }

                        localPageRanges.Add(new PageRange(
                            pageRange.startAddress, pageRange.endAddress,
                            pageRange.protection));
                    }

                    pageRanges = localPageRanges;
                    return ReturnCode.Ok;
                }
                else
                {
                    error = String.Format(
                        "could not read any lines from {0}",
                        Utility.FormatWrapOrNull(fileName));
                }
            }
            catch (Exception e)
            {
                error = e;
            }

            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the page-aligned address and length spanning the given
        /// region on Unix.
        /// </summary>
        /// <param name="address">
        /// The region start address.
        /// </param>
        /// <param name="length">
        /// The length, in bytes.
        /// </param>
        /// <param name="pageAddress">
        /// On output, receives the page-aligned address.
        /// </param>
        /// <param name="pageLength">
        /// On output, receives the page-aligned length.
        /// </param>
        private static void UnixGetPageAddressRange(
            IntPtr address,         /* in */
            int length,             /* in */
            out IntPtr pageAddress, /* out */
            out UIntPtr pageLength  /* out */
            )
        {
            long pageStart = 0;
            long pageEnd = 0;

            /* NO RESULT */
            GetPageAddressRange(
                address, length, UnixPageSize(), ref pageStart,
                ref pageEnd);

            pageAddress = new IntPtr(pageStart);
            pageLength = (UIntPtr)(pageEnd - pageStart);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Restores the original protection of a memory region on Unix,
        /// complaining on failure rather than throwing.
        /// </summary>
        /// <param name="address">
        /// The region address.
        /// </param>
        /// <param name="length">
        /// The length, in bytes.
        /// </param>
        /// <param name="protection">
        /// The protection to restore.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool UnixResetPageProtectionOrComplain(
            IntPtr address, /* in */
            UIntPtr length, /* in */
            int protection  /* in */
            )
        {
            Thread.MemoryBarrier();

            if (UNM.mprotect(address, length, protection) == 0)
            {
                return true;
            }
            else
            {
                Utility.Complain(
                    null, ReturnCode.Error, String.Format(
                    "mprotect({0}, {1}, {2}) error: {3}",
                    address, length, protection,
                    Utility.GetErrorMessage()));

                return false;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current protection of a region and computes the writable
        /// protection to apply before patching on Unix.
        /// </summary>
        /// <param name="address">
        /// The region address.
        /// </param>
        /// <param name="oldProtection">
        /// On output, receives the current protection.
        /// </param>
        /// <param name="newProtection">
        /// On output, receives the writable protection.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool UnixTryGetPageProtections(
            IntPtr address,        /* in */
            out int oldProtection, /* out */
            out int newProtection  /* out */
            )
        {
            bool result;

            if (UnixTryGetPageProtection(address, out oldProtection))
            {
                result = true;
            }
            else
            {
                oldProtection = UNM.PROT_RX; /* FALLBACK DEFAULT */
                result = false;
            }

            newProtection = oldProtection | UNM.PROT_WRITE;
            return result;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current protection of a memory region on Unix.
        /// </summary>
        /// <param name="address">
        /// The region address.
        /// </param>
        /// <param name="protection">
        /// On output, receives the current protection.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool UnixTryGetPageProtection(
            IntPtr address,    /* in */
            out int protection /* out */
            )
        {
            if (Utility.IsLinuxOperatingSystem())
                return LinuxTryGetPageProtection(address, out protection);

            protection = UNM.PROT_NONE;
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the current protection of a memory region on Linux by
        /// consulting the page-map file.
        /// </summary>
        /// <param name="address">
        /// The region address.
        /// </param>
        /// <param name="protection">
        /// On output, receives the current protection.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool LinuxTryGetPageProtection(
            IntPtr address,    /* in */
            out int protection /* out */
            )
        {
            IEnumerable<PageRange> pageRanges = null;
            Result error = null;

            if (GetPageRanges(
                    linuxMemoryPageMapFileName, ref pageRanges,
                    ref error) == ReturnCode.Ok)
            {
                if (IntPtr.Size == sizeof(long))
                {
                    long value = address.ToInt64();

                    foreach (PageRange pageRange in pageRanges)
                    {
                        if (value < pageRange.startAddress.ToInt64())
                            continue;

                        if (value >= pageRange.endAddress.ToInt64())
                            continue;

                        protection = pageRange.protection;
                        return true;
                    }
                }
                else
                {
                    int value = address.ToInt32();

                    foreach (PageRange pageRange in pageRanges)
                    {
                        if (value < pageRange.startAddress.ToInt32())
                            continue;

                        if (value >= pageRange.endAddress.ToInt32())
                            continue;

                        protection = pageRange.protection;
                        return true;
                    }
                }
            }
            else
            {
                Utility.DebugTrace(String.Format(
                    "LinuxTryGetPageProtection({0}) error: {1}",
                    address, error), typeof(HookOps).Name,
                    TracePriority.Highest);
            }

            protection = UNM.PROT_NONE;
            return false;
        }

        ///////////////////////////////////////////////////////////////////////

#if NET_STANDARD_20 && NET_STANDARD_21
        /// <summary>
        /// Atomically writes a pointer-sized value to the given address.
        /// </summary>
        /// <param name="address">
        /// The destination address.
        /// </param>
        /// <param name="value">
        /// The pointer-sized value to write.
        /// </param>
        private static unsafe void AtomicWriteIntPtr(
            IntPtr address, /* in */
            IntPtr value    /* in */
            )
        {
            if (IntPtr.Size == sizeof(long))
            {
                Interlocked.Exchange(
                    ref *(long*)address.ToPointer(), value.ToInt64());
            }
            else
            {
                Interlocked.Exchange(
                    ref *(int*)address.ToPointer(), value.ToInt32());
            }
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes patch bytes using the Bolt helper library (macOS).
        /// </summary>
        /// <param name="address">
        /// The destination address.
        /// </param>
        /// <param name="patch">
        /// The patch bytes.
        /// </param>
        /// <param name="length">
        /// The length, in bytes.
        /// </param>
        private static void BoltRawWrite(
            IntPtr address, /* in */
            byte[] patch,   /* in */
            int length      /* in */
            )
        {
            GCHandle handle = default(GCHandle);

            try
            {
                handle = GCHandle.Alloc(patch, GCHandleType.Pinned);

                if (handle.IsAllocated)
                {
                    if (UNM.write_code_patch(
                            address, handle.AddrOfPinnedObject(),
                            new UIntPtr((ulong)length)) == 0)
                    {
                        throw new ScriptException(
                            Utility.GetErrorMessage());
                    }
                }
                else
                {
                    throw new ScriptException(
                        "could not allocate pinned GC handle");
                }
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                    handle = default(GCHandle);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes patch bytes directly to memory on Unix, toggling JIT write
        /// protection as needed.
        /// </summary>
        /// <param name="address">
        /// The destination address.
        /// </param>
        /// <param name="patch">
        /// The patch bytes.
        /// </param>
        /// <param name="length">
        /// The length, in bytes.
        /// </param>
        private static void UnixRawWrite(
            IntPtr address, /* in */
            byte[] patch,   /* in */
            int length      /* in */
            )
        {
            Thread.MemoryBarrier();

#if NET_STANDARD_20 && NET_STANDARD_21
            if (length == IntPtr.Size)
            {
                AtomicWriteIntPtr(address, new IntPtr(
                    IntPtr.Size == sizeof(long) ?
                        BitConverter.ToInt64(patch, 0) :
                        BitConverter.ToInt32(patch, 0)));
            }
            else
#endif
            {
                switch (length)
                {
                    case sizeof(int):
                        {
                            Marshal.WriteInt32(address,
                                BitConverter.ToInt32(patch, 0));

                            break;
                        }
                    case sizeof(long):
                        {
                            Marshal.WriteInt64(address,
                                BitConverter.ToInt64(patch, 0));

                            break;
                        }
                    default:
                        {
                            Marshal.Copy(
                                patch, 0, address, length);

                            break;
                        }
                }
            }

            Thread.MemoryBarrier();

            //
            // HACK: Technically, the following method call is the only
            //       reason that this method must include "Unix" in its
            //       name, i.e. because none of the other method calls
            //       (or other code) within this method care about the
            //       specific operating system.
            //
            if (!UnixFlushInstructionCache(address, length))
            {
                Utility.DebugTrace(String.Format(
                    "UnixFlushInstructionCache({0}, {1}) error: {2}",
                    address, length, Utility.GetErrorMessage()),
                    typeof(HookOps).Name, TracePriority.Highest);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes bytes to the target memory using the Unix memory API,
        /// adjusting page protection and flushing the instruction cache as
        /// needed.
        /// </summary>
        /// <param name="process">
        /// A handle to the process whose memory is accessed.
        /// </param>
        /// <param name="address">
        /// The target memory address.
        /// </param>
        /// <param name="patch">
        /// The bytes to write.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode UnixWrite(
            IntPtr process,  /* in */
            IntPtr address,  /* in */
            byte[] patch,    /* in */
            ref Result error /* out */
            )
        {
            if (address == IntPtr.Zero)
            {
                error = "invalid write address";
                return ReturnCode.Error;
            }

            if (patch == null)
            {
                error = "invalid patch";
                return ReturnCode.Error;
            }

            int length = patch.Length;

            if (length <= 0) /* IMPOSSIBLE (?) */
            {
                error = "invalid patch length";
                return ReturnCode.Error;
            }

            IntPtr pageAddress;
            UIntPtr pageLength;

            /* NO RESULT */
            UnixGetPageAddressRange(
                address, length, out pageAddress, out pageLength);

            try
            {
                if (Utility.IsMacintoshOperatingSystem() &&
                    (UNM.pthread_jit_write_protect_supported_np() != 0))
                {
                    /* NO RESULT */
                    BoltRawWrite(address, patch, length); /* throw */
                }
                else
                {
                    int oldProtection;
                    int newProtection;

                    /* IGNORED */
                    UnixTryGetPageProtections(
                        pageAddress, out oldProtection, out newProtection);

                    try
                    {
                        if (UNM.mprotect(
                                pageAddress, pageLength, newProtection) != 0)
                        {
                            error = Utility.GetErrorMessage();

                            Utility.Complain(
                                null, ReturnCode.Error, String.Format(
                                "mprotect({0}, {1}, {2}) error: {3}",
                                pageAddress, pageLength, newProtection,
                                error));

                            return ReturnCode.Error;
                        }

                        /* NO RESULT */
                        UnixRawWrite(address, patch, length);
                    }
                    finally
                    {
                        /* IGNORED */
                        UnixResetPageProtectionOrComplain(
                            pageAddress, pageLength, oldProtection);
                    }
                }

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Flushes the instruction cache for a region on Unix.
        /// </summary>
        /// <param name="address">
        /// The region address.
        /// </param>
        /// <param name="length">
        /// The length, in bytes.
        /// </param>
        /// <returns>
        /// Non-zero on success; otherwise, zero.
        /// </returns>
        private static bool UnixFlushInstructionCache(
            IntPtr address, /* in */
            int length      /* in */
            )
        {
            _Arch processor = Utility.GetProcessorArchitecture();

            if ((processor != _Arch.ARM) &&
                (processor != _Arch.ARM64))
            {
                //
                // NOTE: On x86 (Intel) and x64 (AMD64) the instruction and
                //       data caches are coherent; no explicit instruction-
                //       cache flush is required, so this is treated as
                //       success (i.e. there is nothing to do).  For any
                //       other, unexpected architecture, report that no flush
                //       was performed so the caller can trace it.
                //
                return (processor == _Arch.Intel) ||
                    (processor == _Arch.AMD64);
            }

            UIntPtr localLength = new UIntPtr((uint)length);

            if (Utility.IsMacintoshOperatingSystem())
            {
                try
                {
                    /* NO RESULT */
                    UNM.internal_sys_icache_invalidate(
                        address, localLength);

                    return true;
                }
                catch
                {
                    // do nothing.
                }

                try
                {
                    /* NO RESULT */
                    UNM.system_sys_icache_invalidate(
                        address, localLength);

                    return true;
                }
                catch
                {
                    // do nothing.
                }
            }

            try
            {
                UNM.ClearCacheDelegate clearCache =
                    UnixResolveClearCache();

                if (clearCache != null)
                {
                    /* NO RESULT */
                    clearCache(address, GetEndAddress(
                        address, localLength));

                    return true;
                }
            }
            catch
            {
                // do nothing.
            }

            return false;
        }
#endif

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Reads bytes from the target memory, dispatching to the Windows or
        /// Unix implementation for the current operating system.
        /// </summary>
        /// <param name="process">
        /// A handle to the process whose memory is accessed.
        /// </param>
        /// <param name="address">
        /// The target memory address.
        /// </param>
        /// <param name="patch">
        /// The buffer that receives the bytes read.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode Read(
            IntPtr process,  /* in */
            IntPtr address,  /* in */
            byte[] patch,    /* in, out */
            ref Result error /* out */
            )
        {
#if WINDOWS
            if (Utility.IsWindowsOperatingSystem())
                return WindowsRead(process, address, patch, ref error);
#endif

#if UNIX
            if (Utility.IsUnixOperatingSystem())
                return UnixRead(process, address, patch, ref error);
#endif

            error = "not supported on this operating system";
            return ReturnCode.Error;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Writes bytes to the target memory, dispatching to the Windows or
        /// Unix implementation for the current operating system.
        /// </summary>
        /// <param name="process">
        /// A handle to the process whose memory is accessed.
        /// </param>
        /// <param name="address">
        /// The target memory address.
        /// </param>
        /// <param name="patch">
        /// The bytes to write.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        private static ReturnCode Write(
            IntPtr process,  /* in */
            IntPtr address,  /* in */
            byte[] patch,    /* in */
            ref Result error /* out */
            )
        {
            //
            // NOTE: The patch bytes are written directly into the live,
            //       executing method without first suspending the other
            //       managed threads.  Suspending managed threads here is
            //       deliberately avoided because doing so can deadlock
            //       against the garbage collector and the runtime.  As a
            //       result, callers MUST serialize concurrent Start and Stop
            //       operations that target the same method, and a small,
            //       inherent race window remains for the multi-byte (non-
            //       atomic) patch kinds should another thread execute the
            //       method prologue at the exact moment it is overwritten.
            //       Pointer-slot patches use an atomic write where the
            //       runtime tier supports it (see UnixRawWrite) to avoid
            //       torn reads in the common case.
            //
#if WINDOWS
            if (Utility.IsWindowsOperatingSystem())
                return WindowsWrite(process, address, patch, ref error);
#endif

#if UNIX
            if (Utility.IsUnixOperatingSystem())
                return UnixWrite(process, address, patch, ref error);
#endif

            error = "not supported on this operating system";
            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Initializes the hooking engine, building the patch and jump-stub
        /// tables and resolving the dynamic-method descriptor member.  This is
        /// idempotent unless forced.
        /// </summary>
        /// <param name="force">
        /// Non-zero to force re-initialization even when already initialized.
        /// </param>
        public static void Initialize(
            bool force /* in */
            )
        {
            lock (syncRoot) /* TRANSACTIONAL */
            {
                if (force || (patches == null))
                    InitializePatches(ref patches);

                if (force || (jumpStubs == null))
                    InitializeJumpStubs(ref jumpStubs);

                if (force || (descriptorMember == null))
                {
                    InitializeDescriptorMember(
                        typeof(DynamicMethod),
                        ref descriptorMember);
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Looks up a method on a type by name and binding flags.
        /// </summary>
        /// <param name="type">
        /// The type to search.
        /// </param>
        /// <param name="name">
        /// The method name.
        /// </param>
        /// <param name="bindingFlags">
        /// The binding flags controlling the search.
        /// </param>
        /// <param name="method">
        /// On output, receives the resolved method.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode LookupMethodBase(
            Type type,                 /* in */
            string name,               /* in */
            BindingFlags bindingFlags, /* in */
            ref MethodBase method,     /* out */
            ref Result error           /* out */
            )
        {
            if (type == null)
            {
                error = "invalid method type";
                return ReturnCode.Error;
            }

            if (name == null)
            {
                error = "invalid method name";
                return ReturnCode.Error;
            }

            try
            {
                method = type.GetMethod(
                    name, bindingFlags); /* throw */

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the return type and parameter types of a method, appending the
        /// parameter types to the supplied list.
        /// </summary>
        /// <param name="method">
        /// The method to inspect.
        /// </param>
        /// <param name="returnType">
        /// On output, receives the return type.
        /// </param>
        /// <param name="parameterTypes">
        /// On input, an optional list to append to; on output, the list of
        /// parameter types.
        /// </param>
        public static void GetReturnAndParameterTypes(
            MethodBase method,          /* in */
            ref Type returnType,        /* in, out */
            ref TypeList parameterTypes /* in, out */
            )
        {
            if (method == null)
                return;

            TypeList localParameterTypes = parameterTypes;
            ParameterInfo[] parameters = method.GetParameters();

            if (parameters != null)
            {
                if (localParameterTypes == null)
                    localParameterTypes = new TypeList();

                foreach (ParameterInfo parameter in parameters)
                {
                    if (parameter == null)
                        continue;

                    localParameterTypes.Add(parameter.ParameterType);
                }
            }

            Type localReturnType = returnType;
            MethodInfo methodInfo = method as MethodInfo;

            if (methodInfo != null)
                localReturnType = methodInfo.ReturnType;

            returnType = localReturnType;
            parameterTypes = localParameterTypes;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Builds the patch flags from the optional allow-legacy and
        /// allow-fallback selectors, leaving the flags unchanged when both are
        /// unspecified.
        /// </summary>
        /// <param name="allowLegacy">
        /// Whether to allow legacy patching, or null to leave unspecified.
        /// </param>
        /// <param name="allowFallback">
        /// Whether to allow fallback, or null to leave unspecified.
        /// </param>
        /// <param name="patchFlags">
        /// On output, receives the resulting patch flags when any selector is
        /// specified.
        /// </param>
        public static void ChangePatchFlags(
            bool? allowLegacy,         /* in: OPTIONAL */
            bool? allowFallback,       /* in: OPTIONAL */
            ref PatchFlags? patchFlags /* in, out: OPTIONAL */
            )
        {
            if ((allowLegacy == null) && (allowFallback == null))
                return;

            PatchFlags localPatchFlags = PatchFlags.None;

            if ((allowLegacy != null) && (bool)allowLegacy)
                localPatchFlags |= PatchFlags.AllowLegacy;

            if ((allowFallback != null) && (bool)allowFallback)
                localPatchFlags |= PatchFlags.AllowFallback;

            patchFlags = localPatchFlags;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Installs a hook that redirects calls from the old method to the new
        /// method.  The method handles are prepared, the target code address
        /// is resolved (following jump-stubs as needed), the original bytes
        /// are saved, the patch is applied, and a
        /// <see cref="HookClientData" /> tracking the hook is returned
        /// via the caller data.
        /// </summary>
        /// <param name="oldMethod">
        /// The original method to hook.
        /// </param>
        /// <param name="newMethod">
        /// The replacement method to redirect to.
        /// </param>
        /// <param name="processor">
        /// The processor architecture, or unknown to detect it.
        /// </param>
        /// <param name="maximumFollow">
        /// The maximum number of jump-stubs to follow, or non-positive to use
        /// the default.
        /// </param>
        /// <param name="patchFlags">
        /// The optional patch flags controlling legacy and fallback behavior.
        /// </param>
        /// <param name="clientData">
        /// On input, must be null; on success, receives the hook tracking data
        /// used to later stop the hook.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode Start(
            MethodBase oldMethod,       /* in */
            MethodBase newMethod,       /* in */
            _Arch processor,            /* in */
            int maximumFollow,          /* in */
            PatchFlags? patchFlags,     /* in */
            ref IClientData clientData, /* in, out */
            ref Result error            /* out */
            )
        {
            if (oldMethod == null)
            {
                error = "invalid old method";
                return ReturnCode.Error;
            }

            if (newMethod == null)
            {
                error = "invalid new method";
                return ReturnCode.Error;
            }

            if (clientData != null)
            {
                error = "cannot overwrite valid clientData";
                return ReturnCode.Error;
            }

            if (processor == _Arch.Unknown)
                processor = Utility.GetProcessorArchitecture();

            if (maximumFollow <= 0)
                maximumFollow = defaultMaximumFollow;

            bool allowLegacy;
            bool allowFallback;

            /* NO RESULT */
            GetPatchFlags(processor,
                patchFlags, out allowLegacy, out allowFallback);

            RuntimeMethodHandle oldHandle;
            RuntimeMethodHandle newHandle;

            if (!GetRuntimeMethodHandle(oldMethod, out oldHandle))
            {
                error = "invalid old method handle";
                return ReturnCode.Error;
            }

            if (!GetRuntimeMethodHandle(newMethod, out newHandle))
            {
                error = "invalid new method handle";
                return ReturnCode.Error;
            }

            RuntimeHelpers.PrepareMethod(oldHandle);
            RuntimeHelpers.PrepareMethod(newHandle);

            try
            {
                IntPtr oldPointer = oldHandle.GetFunctionPointer();

                if (oldPointer == IntPtr.Zero)
                {
                    error = "invalid old function pointer";
                    return ReturnCode.Error;
                }

                IntPtr newPointer = newHandle.GetFunctionPointer();

                if (newPointer == IntPtr.Zero)
                {
                    error = "invalid new function pointer";
                    return ReturnCode.Error;
                }

                IntPtr process = GetCurrentProcess();
                byte[] savedPatch; /*  READ: ASM code -OR- address */
                byte[] applyPatch; /* WRITE: ASM code -OR- address */
                int patchLength;   /*  BOTH: length */
                PatchKind kind; /* REUSED */

                if (ShouldTryToUseJumpStubs(processor))
                {
                    Result localError = null;

                    if (processor == _Arch.ARM64) /* SPECIAL */
                    {
                        kind = PatchKind.None;

                        if (ARM64.TryToResolve(
                                process, maximumFollow,
                                allowFallback, ref oldPointer,
                                ref kind, ref localError))
                        {
                            if (kind == PatchKind.AbsoluteAddress)
                            {
                                goto patch;
                            }
                            else
                            {
                                error = String.Format(
                                    "cannot use {0} for processor: {1}",
                                    kind, processor);

                                return ReturnCode.Error;
                            }
                        }
                        else
                        {
                            error = localError;
                            return ReturnCode.Error;
                        }
                    }
                    else /* AMD64 (?) */
                    {
                        kind = PatchKind.None;

                        if (GetAndTryToResolveWithJumpStub(
                                process, processor, maximumFollow,
                                ref oldPointer, ref kind, ref localError))
                        {
                            if ((kind == PatchKind.RelativeAddress) ||
                                (kind == PatchKind.AbsoluteAddress))
                            {
                                applyPatch = GetAddressBytes(newPointer);
                            }
                            else
                            {
                                error = String.Format(
                                    "unsupported jump-stub patch kind {0}",
                                    kind);

                                return ReturnCode.Error;
                            }

                            patchLength = applyPatch.Length;
                            savedPatch = new byte[patchLength];

                            if (Read(
                                    process, oldPointer, savedPatch,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }

                            if (Write(
                                    process, oldPointer, applyPatch,
                                    ref error) != ReturnCode.Ok)
                            {
                                return ReturnCode.Error;
                            }

                            clientData = new HookClientData(
                                new HookData(processor, oldMethod,
                                newMethod, oldHandle, newHandle,
                                oldPointer, newPointer, kind,
                                savedPatch, applyPatch, true),
                                null);

                            return ReturnCode.Ok;
                        }
                        else if (!allowLegacy)
                        {
                            error = localError;
                            return ReturnCode.Error;
                        }
                    }
                }

            patch:

                applyPatch = CreatePatch(
                    processor, newPointer, ref error);

                if (applyPatch == null)
                    return ReturnCode.Error;

                patchLength = applyPatch.Length;
                savedPatch = new byte[patchLength];

                if (Read(
                        process, oldPointer, savedPatch,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                if (Write(
                        process, oldPointer, applyPatch,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                kind = PatchKind.FullTrampoline;

                clientData = new HookClientData(
                    new HookData(processor, oldMethod,
                    newMethod, oldHandle, newHandle,
                    oldPointer, newPointer, kind,
                    savedPatch, applyPatch, true),
                    null);

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                Utility.DebugTrace(
                    e, typeof(HookOps).Name,
                    TracePriority.Highest |
                        TracePriority.FromPlugin);

                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Removes a previously installed hook, restoring the original method
        /// bytes, using the hook tracking data produced by <c>Start</c>.
        /// </summary>
        /// <param name="clientData">
        /// On input, the hook tracking data; cleared on success.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode Stop(
            ref IClientData clientData, /* in, out */
            ref Result error            /* out */
            )
        {
            HookClientData hookClientData = clientData as HookClientData;

            if (hookClientData == null)
            {
                error = "invalid hook clientData";
                return ReturnCode.Error;
            }

            HookData hookData = hookClientData.HookData;

            if (hookData == null)
            {
                error = "invalid hook data";
                return ReturnCode.Error;
            }

            MethodBase oldMethod = hookData.OldMethod;

            if (oldMethod == null)
            {
                error = "invalid old method";
                return ReturnCode.Error;
            }

            byte[] savedPatch = hookData.SavedPatch;

            if (savedPatch == null)
            {
                error = "invalid saved patch";
                return ReturnCode.Error;
            }

            if (!hookData.Active)
            {
                error = "hook is not active";
                return ReturnCode.Error;
            }

            RuntimeMethodHandle oldHandle;

            if (!GetRuntimeMethodHandle(oldMethod, out oldHandle))
            {
                error = "invalid old method handle";
                return ReturnCode.Error;
            }

            try
            {
                IntPtr oldPointer = hookData.OldPointer;

                if (oldPointer == IntPtr.Zero)
                {
                    error = "invalid old function pointer";
                    return ReturnCode.Error;
                }

                IntPtr process = GetCurrentProcess();

                if (Write(
                        process, oldPointer, savedPatch,
                        ref error) != ReturnCode.Ok)
                {
                    return ReturnCode.Error;
                }

                hookData.Active = false;
                clientData = null;

                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
                Utility.DebugTrace(
                    e, typeof(HookOps).Name,
                    TracePriority.Highest |
                        TracePriority.FromPlugin);

                error = e;
                return ReturnCode.Error;
            }
        }
        #endregion
    }
}
