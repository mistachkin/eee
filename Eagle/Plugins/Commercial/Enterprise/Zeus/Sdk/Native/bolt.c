/*
 * bolt.c --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if defined(_WIN32)
#  define BOLT_EXPORT		__declspec(dllexport)
#else
#  define BOLT_EXPORT
#endif

#if defined(__APPLE__)
#  ifndef PROT_RX
#    define PROT_RX		(PROT_READ | PROT_EXEC)
#  endif
#  ifndef PROT_RWX
#    define PROT_RWX		(PROT_READ | PROT_WRITE | PROT_EXEC)
#  endif
#  ifndef PAGE_MASK
#    define PAGE_MASK(a, ps)	(((uintptr_t)(a)) & (~(uintptr_t)((ps) - 1)))
#  endif
#endif

#include <stdio.h>			/* NOTE: For snprintf, etc. */
#include <stddef.h>			/* NOTE: For size_t, etc. */

#if defined(__APPLE__)
#  include <string.h>			/* NOTE: For memcpy, etc. */
#  include <pthread.h>			/* NOTE: For pthread_*, etc. */
#  include <unistd.h>			/* NOTE: For sysconf, etc. */
#  include <sys/mman.h>			/* NOTE: For mprotect, etc. */
#  include <libkern/OSCacheControl.h>	/* NOTE: For sys_icache_invalidate. */
#endif

#if defined(__APPLE__)
/*
 *----------------------------------------------------------------------
 *
 * mprotect_page --
 *
 *	This function attempts to change the memory protection bits for
 *	the entire page at the specified address.
 *
 * Results:
 *	Zero upon success; otherwise, non-zero.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

BOLT_EXPORT int mprotect_page(
    void *addr,			/* The address within the target page. */
    size_t len,			/* Number of bytes to change protection
				 * bits for. */
    int prot)			/* The set of desired protection bits. */
{
    long pageSize = sysconf(_SC_PAGESIZE);
    uintptr_t baseAddr = PAGE_MASK(addr, pageSize);
    size_t spanSize = (((uintptr_t)baseAddr) + len + (pageSize - 1));

    spanSize = PAGE_MASK(spanSize, pageSize);
    spanSize -= baseAddr;

    return mprotect((void *)baseAddr, spanSize, prot);
}

/*
 *----------------------------------------------------------------------
 *
 * write_code_patch --
 *
 *	This function attempts to write (i.e. copy) the specified code
 *	(patch) bytes to the specified address.
 *
 * Results:
 *	Non-zero upon success; otherwise, zero.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

BOLT_EXPORT int write_code_patch(
    void *dst,			/* The starting address where the code
				 * patch should be written. */
    const void *src,		/* The starting address where the code
				 * patch should be read. */
    size_t len)			/* Size of the code patch, in bytes. */
{
    int bSupported = pthread_jit_write_protect_supported_np();

    if (bSupported) {
        pthread_jit_write_protect_np(0);
    } else if (mprotect_page(dst, len, PROT_RWX) != 0) {
        return 0;
    }

    memcpy(dst, src, len);
    sys_icache_invalidate(dst, len);

    if (bSupported) {
        pthread_jit_write_protect_np(1);
    } else if (mprotect_page(dst, len, PROT_RX) != 0) {
        return 0;
    }

    return 1;
}
#endif /* defined(__APPLE__) */

/*
 *----------------------------------------------------------------------
 *
 * bolt_snprintf_double --
 *
 *	This function formats a single double-precision floating point
 *	value into the specified buffer using the specified printf-style
 *	format string.  It exists so that the (variadic) snprintf() can
 *	be called from native code, where platform calling conventions
 *	for variadic functions are handled correctly by the compiler.
 *	The managed runtime cannot reliably express the variadic calling
 *	convention via a fixed P/Invoke signature on some platforms (e.g.
 *	arm64), which can yield incorrect results.
 *
 * Results:
 *	The return value of snprintf(), i.e. the number of characters
 *	that would have been written had the buffer been large enough.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

BOLT_EXPORT int bolt_snprintf_double(
    char *buffer,		/* The output buffer. */
    size_t count,		/* Size of output buffer, in bytes. */
    const char *format,		/* The printf-style format string. */
    double value)		/* The value to be formatted. */
{
    return snprintf(buffer, count, format, value);
}
