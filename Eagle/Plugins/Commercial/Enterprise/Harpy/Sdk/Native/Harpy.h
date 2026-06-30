/*
 * Harpy.h --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#ifndef _HARPY_H_
#define _HARPY_H_

#if defined(_WIN32)
#  if !defined(MIN_STR_BUF)
#    define MIN_STR_BUF				((MAX_PATH) + 32)
#  endif

#  if !defined(MAX_STR_BUF)
#    define MAX_STR_BUF				(1024)
#  endif

#  if !defined(TKT_ID_BUF)
#    define TKT_ID_BUF				(38)
#  endif

#  if !defined(HARPY_VVA_ONLY)
#    ifndef NDEBUG
#      define HARPY_VVA_ONLY(X)			X
#    else
#      define HARPY_VVA_ONLY(X)
#    endif
#  endif

#  if defined(_DEBUG) || defined(HARPY_DEBUG)
#    if !defined(_MSC_VER) || (_MSC_VER < 1900)
#      define __func__				"<unknown>"
#    endif
#
#    if defined(HARPY_SELF_TEST) && defined(HARPY_TEST_EXE)
#      define MaybeOutputString(x) {                                \
	    OutputDebugStringA((x));                                \
	    AppendToLogFileA((x));                                  \
	    fprintf(stdout, "eeeSdk1: %s", (x));                    \
	} /* MaybeOutputString */
#
#      define MaybeOutputHResult(x) {                               \
	    if ((x) != S_OK) {                                      \
		char buf[MIN_STR_BUF + 1]; /* "FFFFFFFFFFFFFFFF" */ \
		memset(buf, 0, sizeof(buf));                        \
		_snprintf(buf, sizeof(buf),                         \
		    "eeeSdk1: %s HRESULT 0x%016X\n",                \
		    __func__, (x));                                 \
		fprintf(stdout, "%s", buf);                         \
		OutputDebugStringA(buf);                            \
		AppendToLogFileA(buf);                              \
	    }                                                       \
	} /* MaybeOutputHResult */
#    else
#      define MaybeOutputString(x) {                                \
	    OutputDebugStringA((x));                                \
	    AppendToLogFileA((x));                                  \
	} /* MaybeOutputString */
#
#      define MaybeOutputHResult(x) {                               \
	    if ((x) != S_OK) {                                      \
		char buf[MIN_STR_BUF + 1]; /* "FFFFFFFFFFFFFFFF" */ \
		memset(buf, 0, sizeof(buf));                        \
		_snprintf(buf, sizeof(buf),                         \
		    "eeeSdk1: %s HRESULT 0x%016X\n",                \
		    __func__, (x));                                 \
		OutputDebugStringA(buf);                            \
		AppendToLogFileA(buf);                              \
	    }                                                       \
	} /* MaybeOutputHResult */
#    endif
#  else
#    define MaybeOutputString(x)
#    define MaybeOutputHResult(x)
#  endif

#  if !defined(HEX_TO_INT)
#    define HEX_TO_INT(x)	(((x) + (9 * (1 & ((x) >> 6)))) & 0xF)
#  endif

#  if !defined(MAYBE_MUTATE)
#    define MAYBE_MUTATE(i)	(((i) & 0xFF) == 0) ? ((~(i)) & ~85L) : (i)
#  endif

#  if !defined(OBFUSCATE)
#    define OBFUSCATE(x,i,m)	((((x)[(i)]) ^ ((m) & (85L << (i)))) & 0xFF)
#  endif

#  if !defined(SEVERITY_ERROR)
#    define SEVERITY_ERROR			(1)
#  endif

#  if !defined(FACILITY_CUSTOMER_BIT)
#    define FACILITY_CUSTOMER_BIT		(0x20000000)
#  endif

#  if !defined(FACILITY_CRT)
#    define FACILITY_CRT			(76)
#  endif

#  if !defined(FACILITY_CUSTOMER_CRT)
#    define FACILITY_CUSTOMER_CRT \
			(((unsigned long)(FACILITY_CUSTOMER_BIT)) | \
			(((unsigned long)(FACILITY_CRT)) << 16))
#endif

#  if !defined(HRESULT_FROM_ERRNO)
#    define HRESULT_FROM_ERRNO(x) \
		((HRESULT)((((unsigned long)(SEVERITY_ERROR)) << 31) | \
		(FACILITY_CUSTOMER_CRT) | ((x) & 0xFFFF)))
#  endif

#  if !defined(FUSION_E_SIGNATURE_CHECK_FAILED)
#    define FUSION_E_SIGNATURE_CHECK_FAILED	(0x80131045)
#  endif

#  if !defined(ENOERR)
#    define ENOERR				(0)
#  endif

#  define STRINGIFY(x)				STRINGIFY1(x)
#  define STRINGIFY1(x)				#x

#  define UNICODE_TEXT(x)			UNICODE_TEXT1(x)
#  define UNICODE_TEXT1(x)			L##x

#  define CORE_CLR_MODULE_NAME			"CoreCLR"
#  define CLR_MODULE_NAME			"MSCorEE"

#  if defined(USE_CORE_CLR)
#    define CORE_CLR_PROC_NAME			"GetCLRRuntimeHost"
#  endif

#  if defined(USE_CLR_40)
#    define CLR_PROC_NAME			"CLRCreateInstance"
#    define CLR_VERSION_V2			"v2.0.50727"
#    define CLR_VERSION_V4			"v4.0.30319"
#    define CLR_VERSION_MINIMUM			CLR_VERSION_V2
#    define CLR_VERSION_LATEST			CLR_VERSION_V4
#  endif

#  if !defined(LOG_ENVVAR_NAME)
#    define LOG_ENVVAR_NAME			"HarpyNativeSdkLog"
#  endif

#  if !defined(LOG_FILE_ENVVAR_NAME)
#    define LOG_FILE_ENVVAR_NAME		"HarpyNativeSdkLogFile"
#  endif

#  if !defined(LOG_FILE_SUFFIX)
#    define LOG_FILE_SUFFIX			".HarpyNativeSdk.log"
#  endif

#  if !defined(LICENSE_TICKET_ENVVAR_NAME)
#    define LICENSE_TICKET_ENVVAR_NAME		"HarpyLicenseTicket"
#  endif

#  if !defined(TICKET_ID_LENGTH)
#    define TICKET_ID_LENGTH			(36)
#  endif

#  if !defined(TICKET_ID_ENVVAR_NAME)
#    define TICKET_ID_ENVVAR_NAME		"HarpyTicketId"
#  endif

#  if !defined(ASSEMBLY_PATH_ENVVAR_NAME)
#    define ASSEMBLY_PATH_ENVVAR_NAME		"LicenseAssemblyPath"
#  endif

#  if !defined(OTHER_APPDOMAIN_ENVVAR_NAME)
#    define OTHER_APPDOMAIN_ENVVAR_NAME		"LicenseOtherAppDomain"
#  endif

#  if !defined(CALLBACK_ENVVAR_FORMAT)
#    define CALLBACK_ENVVAR_FORMAT		"SdkCallback_%lX_%lX_%lX"
#  endif

#  if !defined(MANAGED_ASSEMBLY_NAME)
#    error "Missing define 'MANAGED_ASSEMBLY_NAME', it is required."
#  endif

#  if !defined(MANAGED_TYPE_NAME)
#    error "Missing define 'MANAGED_TYPE_NAME', it is required."
#  endif

#  if !defined(MANAGED_METHOD_NAME)
#    define MANAGED_METHOD_NAME			Verify
#  endif

#  if !defined(MANAGED_STRONG_NAME_TOKEN)
#    define MANAGED_STRONG_NAME_TOKEN		\
			{ 0x0A, 0x9A, 0x2A, 0x02, 0x61, 0x4F, 0x8A, 0x52 }
#  endif

#  if !defined(HARPY_API)
#    define HARPY_API				static
#  endif

#  if !defined(HARPY_VERIFY_SUCCESS)
#    define HARPY_VERIFY_SUCCESS		(0)
#  endif

#  if !defined(HARPY_VERIFY_FAILURE)
#    define HARPY_VERIFY_FAILURE		(1)
#  endif

#  if !defined(HARPY_VERIFY_QUEUED)
#    define HARPY_VERIFY_QUEUED			(4)
#  endif

HARPY_API HRESULT	GetSdkInfoStrings(CONST LPCSTR **pazStrings);
HARPY_API HRESULT	AppendToLogFileA(LPSTR message);
HARPY_API HRESULT	VerifyClrIsLoaded(BOOL bUseMinimumClr,
			    ICLRRuntimeHost **ppClrRuntimeHost);
HARPY_API HRESULT	VerifyIsLicensed(BOOL bUseMinimumClr,
			    ICLRRuntimeHost *pClrRuntimeHost);
HARPY_API HRESULT	VerifyClrCleanup(
			    ICLRRuntimeHost **ppClrRuntimeHost);
BOOL WINAPI		DllMain(HINSTANCE hInstance, DWORD reason,
			    LPVOID reserved);

#  if defined(HARPY_SELF_TEST)
HARPY_API HRESULT	VerifySelfTest(VOID);
#  endif
#endif /* defined(_WIN32) */

#endif /* _HARPY_H_ */
