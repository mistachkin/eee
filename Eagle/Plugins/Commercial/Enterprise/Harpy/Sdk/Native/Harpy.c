/*
 * Harpy.c --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#include "HarpyRes.h"				/* NOTE: Resource stuff. */

#if defined(_WIN32)
#  if defined(_DEBUG) || defined(HARPY_DEBUG)
#    include <stdio.h>				/* NOTE: For _snwprintf. */
#  endif
#
#  include <assert.h>				/* NOTE: For assert. */
#  include <string.h>				/* NOTE: For memcmp. */
#  include <stdlib.h>				/* NOTE: For _wtoi64. */
#  include <errno.h>				/* NOTE: For errno. */
#  include "windows.h"				/* NOTE: Base types. */
#
#  if !defined(_DEBUG) || defined(HARPY_SELF_TEST)
#    include "SoftPub.h"			/* NOTE: WinVerifyTrust. */
#  endif
#
#  include "MSCorEE.h"				/* NOTE: CLR v2 API. */
#  include "StrongName.h"			/* NOTE: What it says. */
#
#  if defined(USE_CLR_40)
#    include "MetaHost.h"			/* NOTE: CLR v4 API. */
#  endif
#endif

#include "Harpy.h"				/* NOTE: Private stuff. */

#if defined(_WIN32) && defined(USE_CORE_CLR)
typedef HRESULT (STDAPICALLTYPE *GetCLRRuntimeHostFnPtr)(
    REFIID riid, IUnknown **pUnk
);

const GUID IID_ICLRRuntimeHost2 = {
    0x712AB73F, 0x2C22, 0x4807, {
	0xAD, 0x7E, 0xF5, 0x01, 0xD7, 0xB7, 0x2C, 0x2D
    }
};
#endif /* defined(_WIN32) && defined(USE_CORE_CLR) */

static const char* const harpyOptions[] = {
    "HARPY_NATIVE_SDK " HARPY_SDK_VERSION,
#if defined(_MSC_VER)
    "MSVC v" STRINGIFY(_MSC_VER),
#else /* defined(_MSC_VER) */
    "UNKNOWN_COMPILER",
#endif /* defined(_MSC_VER) */
    "USE_OPTIONS " HARPY_USE_OPTIONS,
    NULL
};

static const unsigned char harpyMagicValue[] = {
    0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 /* MAGIC-VALUE */
};

static const size_t harpyMagicSize =
	sizeof(harpyMagicValue) / sizeof(unsigned char);

#if defined(_WIN32)
static HMODULE hHarpyModule = NULL; /* Needed to get full module path. */
#endif /* defined(_WIN32) */

#if defined(_WIN32)
/*
 *----------------------------------------------------------------------
 *
 * GetSdkInfoStrings --
 *
 *	This function returns a pointer to the array of information
 *	strings about this SDK.  The returned array and its strings
 *	should not be freed or modified in any way.
 *
 * Results:
 *	S_OK for success -OR- an HRESULT indicating a reason for the
 *	failure.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

HARPY_API HRESULT GetSdkInfoStrings(
    CONST LPCSTR **pazStrings)		/* Location to place a pointer to
					 * array of information strings. */
{
    if (pazStrings == NULL) return E_POINTER;
    *pazStrings = harpyOptions;
    return S_OK;
}

/*
 *----------------------------------------------------------------------
 *
 * GetLogFileName --
 *
 *	This function attempts to determine the fully qualified file
 *	name that should be used for logging.
 *
 * Results:
 *	The fully qualified file name to use for logging -OR- NULL if
 *	logging is disabled or if any errors are encountered.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

static LPCWSTR GetLogFileName(VOID)
{
    DWORD size;
    static WCHAR value[MAX_STR_BUF + 1];

    memset(value, 0, sizeof(value));
    size = MAX_STR_BUF;

    if (GetEnvironmentVariableW(
	    UNICODE_TEXT(LOG_ENVVAR_NAME), value, size)) {
	HMODULE hThisModule;
	DWORD nSuffix;

	memset(value, 0, sizeof(value));
	size = MAX_STR_BUF;

	size = GetEnvironmentVariableW(
	    UNICODE_TEXT(LOG_FILE_ENVVAR_NAME), value, size);

	if ((size > 0) && (size <= MAX_STR_BUF)) {
	    return value;
	}

	hThisModule = hHarpyModule;
	memset(value, 0, sizeof(value));
	size = MAX_STR_BUF;

	size = GetModuleFileNameW(hThisModule, value, size);
	nSuffix = lstrlenW(UNICODE_TEXT(LOG_FILE_SUFFIX));

	if ((size > 0) && (size <= (MAX_STR_BUF - nSuffix))) {
	    lstrcatW(value, UNICODE_TEXT(LOG_FILE_SUFFIX));
	    return value;
	}
    }

    return NULL;
}

/*
 *----------------------------------------------------------------------
 *
 * AppendToLogFileA --
 *
 *	This function attempts to append the specified message to the
 *	configured log file, if any.  This function accepts ANSI log
 *	message strings; however, the log file name itself must be a
 *	UNICODE string.
 *
 * Results:
 *	S_OK for success, S_FALSE to indicate nothing was done, -OR-
 *	an HRESULT indicating a reason for the failure.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

HARPY_API HRESULT AppendToLogFileA(
    LPSTR message)			/* The message to append to the
					 * log file. */
{
    HRESULT hResult;

    if (message != NULL) {
	LPCWSTR fileName = GetLogFileName();

	if (fileName != NULL) {
	    FILE *log = _wfopen(fileName, L"ab");

	    if (log == NULL) {
		return HRESULT_FROM_ERRNO(errno);
	    }

	    if (fprintf(log, "%s", message) < 0) {
		return HRESULT_FROM_ERRNO(errno);
	    }

	    if (fflush(log) != 0) {
		return HRESULT_FROM_ERRNO(errno);
	    }

	    if (fclose(log) != 0) {
		return HRESULT_FROM_ERRNO(errno);
	    }

	    hResult = S_OK;
	} else {
	    hResult = S_FALSE;
	}
    } else {
	hResult = E_POINTER;
    }

    return hResult;
}

/*
 *----------------------------------------------------------------------
 *
 * VerifyClrIsLoaded --
 *
 *	This function loads and optionally starts the latest version
 *	of the CLR supported by this package.
 *
 * Results:
 *	S_OK if the CLR is loaded and running in the current process
 *	-OR- an HRESULT indicating a reason for the failure.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

HARPY_API HRESULT VerifyClrIsLoaded(
    BOOL bUseMinimumClr,		/* Force using the minimum
					 * supported CLR version? */
    ICLRRuntimeHost **ppClrRuntimeHost)	/* OUT: Handle to the running
					 * CLR instance. */
{
    HRESULT hResult;
    HMODULE hClrModule;
    ICLRRuntimeHost *pClrRuntimeHost = NULL;

#if defined(USE_CORE_CLR)
    GetCLRRuntimeHostFnPtr pGetCLRRuntimeHost;
#endif /* defined(USE_CORE_CLR) */

#if defined(USE_CLR_40)
    CLRCreateInstanceFnPtr pClrCreateInstance;
    ICLRMetaHost *pClrMetaHost = NULL;
    ICLRRuntimeInfo *pClrRuntimeInfo = NULL;
    LPCWSTR clrVersion;
    BOOL bLoadable = FALSE;
#endif /* defined(USE_CLR_40) */

    hClrModule = GetModuleHandleW(UNICODE_TEXT(CORE_CLR_MODULE_NAME));

    if (hClrModule != NULL) { /* Running on .NET Core? */
	MaybeOutputString("detected .NET Core in process.\n");

#if defined(USE_CORE_CLR)
	pGetCLRRuntimeHost = (GetCLRRuntimeHostFnPtr)GetProcAddress(
	    hClrModule, CORE_CLR_PROC_NAME);

	if (pGetCLRRuntimeHost != NULL) {
	    hResult = pGetCLRRuntimeHost(
		&IID_ICLRRuntimeHost2, (IUnknown **)&pClrRuntimeHost);

	    if (SUCCEEDED(hResult)) {
		assert(pClrRuntimeHost != NULL);

		hResult = ICLRRuntimeHost_Start(pClrRuntimeHost);

		if (SUCCEEDED(hResult)) {
		    MaybeOutputString("ICLRRuntimeHost2 start success.\n");
		    if (ppClrRuntimeHost != NULL) {
			*ppClrRuntimeHost = pClrRuntimeHost;
		    } else {
			ICLRRuntimeHost_Release(pClrRuntimeHost);
			pClrRuntimeHost = NULL;
		    }
		} else {
		    MaybeOutputString("ICLRRuntimeHost2 start failure.\n");
		}
	    } else {
		MaybeOutputString("could not get ICLRRuntimeHost2.\n");
		assert(pClrRuntimeHost == NULL);
	    }

	    goto done;
	} else {
	    MaybeOutputString("missing CoreCLR function.\n");
	    hResult = MSEE_E_GETPROCFAILED;
	    goto done;
	}
#else /* defined(USE_CORE_CLR) */
	hResult = S_FALSE;
	goto done;
#endif /* defined(USE_CORE_CLR) */
    }

#if defined(USE_CLR_40)
    hClrModule = GetModuleHandleW(UNICODE_TEXT(CLR_MODULE_NAME));

    if (hClrModule == NULL) {
	MaybeOutputString("missing CLR module in process.\n");
	hResult = HRESULT_FROM_WIN32(GetLastError());
	goto done;
    }

    pClrCreateInstance = (CLRCreateInstanceFnPtr)GetProcAddress(
	hClrModule, CLR_PROC_NAME);

    if (pClrCreateInstance == NULL) {
	MaybeOutputString("missing CLR function.\n");
	goto fallback; /* Missing CLRv4? */
    }

    hResult = pClrCreateInstance(
	&CLSID_CLRMetaHost, &IID_ICLRMetaHost,
	&pClrMetaHost);

    if (FAILED(hResult)) {
	MaybeOutputString("could not create ICLRMetaHost.\n");
	assert(pClrMetaHost == NULL);

	if (hResult == E_NOTIMPL) {
	    MaybeOutputString("CLR creation not implemented.\n");
	    goto fallback; /* Missing CLRv4? */
	}

	goto done;
    }

    clrVersion = bUseMinimumClr ?
	UNICODE_TEXT(CLR_VERSION_MINIMUM) :
	UNICODE_TEXT(CLR_VERSION_LATEST);

    hResult = ICLRMetaHost_GetRuntime(
	pClrMetaHost, clrVersion, &IID_ICLRRuntimeInfo,
	&pClrRuntimeInfo);

    if (FAILED(hResult)) {
	MaybeOutputString("could not get ICLRRuntimeInfo.\n");
	assert(pClrRuntimeInfo == NULL);
	goto done;
    }

    hResult = ICLRRuntimeInfo_IsLoadable(
	pClrRuntimeInfo, &bLoadable);

    if (FAILED(hResult)) {
	MaybeOutputString("ICLRRuntimeInfo loadable failure.\n");
	goto done;
    }

    if (!bLoadable) {
	MaybeOutputString("ICLRRuntimeInfo not loadable.\n");
	goto done;
    }

    hResult = ICLRRuntimeInfo_GetInterface(
	pClrRuntimeInfo, &CLSID_CLRRuntimeHost,
	&IID_ICLRRuntimeHost, &pClrRuntimeHost);

    if (FAILED(hResult)) {
	MaybeOutputString("could not get ICLRRuntimeHost.\n");
	assert(pClrRuntimeHost == NULL);
	goto done;
    }

    assert(pClrRuntimeHost != NULL);
    MaybeOutputString("ICLRRuntimeHost query success.\n");

#if defined(HARPY_SELF_TEST) && (defined(HARPY_EXE) || defined(HARPY_TEST_EXE))
    hResult = ICLRRuntimeHost_Start(pClrRuntimeHost);

    if (SUCCEEDED(hResult)) {
	MaybeOutputString("ICLRRuntimeHost start success.\n");
    } else {
	MaybeOutputString("ICLRRuntimeHost start failure.\n");
	ICLRRuntimeHost_Release(pClrRuntimeHost);
	pClrRuntimeHost = NULL;
	goto done;
    }
#endif /* defined(HARPY_SELF_TEST) && (defined(HARPY_EXE) || defined(HARPY_TEST_EXE)) */

    if (ppClrRuntimeHost != NULL) {
	*ppClrRuntimeHost = pClrRuntimeHost;
    } else {
	ICLRRuntimeHost_Release(pClrRuntimeHost);
	pClrRuntimeHost = NULL;
    }

    goto done;

fallback:
#endif /* defined(USE_CLR_40) */

    hResult = CorBindToRuntimeEx(
	NULL, NULL, 0, &CLSID_CLRRuntimeHost,
	&IID_ICLRRuntimeHost, &pClrRuntimeHost);

    if (SUCCEEDED(hResult)) {
	assert(pClrRuntimeHost != NULL);
	MaybeOutputString("CorBindToRuntimeEx success.\n");

#if defined(HARPY_SELF_TEST) && (defined(HARPY_EXE) || defined(HARPY_TEST_EXE))
	hResult = ICLRRuntimeHost_Start(pClrRuntimeHost);

	if (SUCCEEDED(hResult)) {
	    MaybeOutputString("ICLRRuntimeHost start success.\n");
	} else {
	    MaybeOutputString("ICLRRuntimeHost start failure.\n");
	    ICLRRuntimeHost_Release(pClrRuntimeHost);
	    pClrRuntimeHost = NULL;
	    goto done;
	}
#endif /* defined(HARPY_SELF_TEST) && (defined(HARPY_EXE) || defined(HARPY_TEST_EXE)) */

	if (ppClrRuntimeHost != NULL) {
	    *ppClrRuntimeHost = pClrRuntimeHost;
	} else {
	    ICLRRuntimeHost_Release(pClrRuntimeHost);
	    pClrRuntimeHost = NULL;
	}
    } else {
	MaybeOutputString("CorBindToRuntimeEx failure.\n");
	assert(pClrRuntimeHost == NULL);
    }

done:

#if defined(USE_CLR_40)
    if (pClrRuntimeInfo != NULL) {
	ICLRRuntimeInfo_Release(pClrRuntimeInfo);
	pClrRuntimeInfo = NULL;
    }

    if (pClrMetaHost != NULL) {
	ICLRMetaHost_Release(pClrMetaHost);
	pClrMetaHost = NULL;
    }
#endif /* defined(USE_CLR_40) */

    MaybeOutputHResult(hResult);
    return hResult;
}

#if !defined(_DEBUG) || defined(HARPY_SELF_TEST)
/*
 *----------------------------------------------------------------------
 *
 * CheckIsTrusted --
 *
 *	This function attempts to check if the specified path is
 *	trusted by the Windows operating system.  If the specified
 *	path is NULL, the loaded module file will be checked.
 *
 * Results:
 *	Zero upon success; otherwise, non-zero.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */
static LONG CheckIsTrusted(
    HANDLE hHeap,		    /* IN: Allocation heap to use. */
    LPCWSTR path,		    /* IN: Path to be checked. */
    HMODULE hModule)		    /* IN: Module handle to query. */
{
    LONG lResult;
    LPWSTR freePath = NULL;
    LPWSTR checkPath = NULL;
    GUID actionId = WINTRUST_ACTION_GENERIC_VERIFY_V2;
    WINTRUST_DATA trustData;
    WINTRUST_FILE_INFO trustFileInfo;

    if (path == NULL) {
	DWORD size = UNICODE_STRING_MAX_CHARS;

	if (hHeap == NULL) {
	    MaybeOutputString("cannot trust, heap invalid.\n");
	    return ERROR_INVALID_HANDLE;
	}

	freePath = HeapAlloc(hHeap, 0, (size + 1) * sizeof(WCHAR));

	if (freePath == NULL) {
	    MaybeOutputString("cannot trust, cannot allocate.\n");
	    return E_OUTOFMEMORY;
	}

	memset(freePath, 0, (size + 1) * sizeof(WCHAR));

	size = GetModuleFileNameW(hModule, freePath, size);

	if (size == 0) {
	    DWORD lastError = GetLastError();

	    MaybeOutputString("cannot trust, no module file name.\n");
	    return lastError ? lastError : ERROR_INVALID_MODULETYPE;
	}

	checkPath = freePath;
    } else {
	checkPath = (LPWSTR)path;
    }

    memset(&trustFileInfo, 0, sizeof(WINTRUST_FILE_INFO));

    trustFileInfo.cbStruct = sizeof(WINTRUST_FILE_INFO);
    trustFileInfo.pcwszFilePath = checkPath;

    memset(&trustData, 0, sizeof(WINTRUST_DATA));

    trustData.cbStruct = sizeof(WINTRUST_DATA);
    trustData.dwUIChoice = WTD_UI_NONE;
    trustData.fdwRevocationChecks = WTD_REVOKE_WHOLECHAIN;
    trustData.dwUnionChoice = WTD_CHOICE_FILE;
    trustData.pFile = &trustFileInfo;
    trustData.dwStateAction = WTD_STATEACTION_IGNORE;

    trustData.dwProvFlags =
	WTD_SAFER_FLAG | WTD_CACHE_ONLY_URL_RETRIEVAL;

    trustData.dwUIContext = WTD_UICONTEXT_EXECUTE;

    lResult = WinVerifyTrust(
	INVALID_HANDLE_VALUE, &actionId, &trustData);

    if (freePath != NULL) {
	HeapFree(hHeap, 0, freePath);
	freePath = NULL;
    }

    return lResult;
}
#endif /* !defined(_DEBUG) || defined(HARPY_SELF_TEST) */

/*
 *----------------------------------------------------------------------
 *
 * MaybeRemoveTailName --
 *
 *	This function attempts to remove the final portion of the
 *	specified path.
 *
 * Results:
 *	Non-zero upon success; otherwise, zero.
 *
 * Side effects:
 *	The specified path buffer is modified in-place.
 *
 *----------------------------------------------------------------------
 */

static BOOL MaybeRemoveTailName(
    LPWSTR path,		    /* IN, OUT: Path to be modified. */
    LPCWSTR matchName)		    /* IN, OPTIONAL: Only if name equals? */
{
    if (path != NULL) {
	int length = lstrlenW(path);
	int index = length - 1;

	for (; index >= 0; index--) {
	    WCHAR c = path[index];

	    if ((c == L'/') || (c == L'\\')) {
		if ((matchName == NULL) ||
			(lstrcmpiW(&path[index + 1], matchName) == 0)) {
		    path[index] = L'\0';
		    return TRUE;
		}
	    }
	}
    }

    return FALSE;
}

/*
 *----------------------------------------------------------------------
 *
 * TryToValidateProcessLicenseTicket --
 *
 *	This function attempts to validate a Harpy license ticket for
 *	the current process.
 *
 * Results:
 *	Non-zero upon success; otherwise, zero.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

static BOOL TryToValidateProcessLicenseTicket(VOID)
{
    BOOL bResult = FALSE;
    HARPY_VVA_ONLY(BOOL bCleanup;)
    CHAR haveTkt[MAX_STR_BUF + 1];
    CHAR wantTkt[MAX_STR_BUF + 1];
    CHAR wantTktId[TKT_ID_BUF + 1];
    unsigned char tktKey[sizeof(DWORD64)];
    DWORD processId = GetCurrentProcessId();
    HCRYPTPROV hProvider = 0;
    HCRYPTHASH hHash = 0;
    HCRYPTKEY hKey = 0;

    assert(sizeof(CHAR) == sizeof(BYTE));
    memset(haveTkt, 0, sizeof(haveTkt));

    if (GetEnvironmentVariableA(
	    LICENSE_TICKET_ENVVAR_NAME, haveTkt, MAX_STR_BUF)) {
	DWORD mix;
	int savedLength = lstrlenA(haveTkt);
	int length = savedLength;
	int index = 0;
	DWORD decryptLength;

#if defined(HARPY_TEST_EXE)
	if (lstrcmpA(haveTkt, "1") == 0) {
	    fprintf(stderr, "please enter ticket for process %d: ",
		GetCurrentProcessId());
	    fgets(haveTkt, MAX_STR_BUF, stdin);
	}
#endif /* defined(HARPY_TEST_EXE) */

	if ((length % 2) != 0) goto done; /* Bad hex digits? */

	for (; index < length; index += 2) {
	    CHAR c1 = haveTkt[index];
	    CHAR c2 = haveTkt[index + 1];

	    assert(isxdigit(c1));
	    assert(isxdigit(c2));

	    haveTkt[index / 2] = (HEX_TO_INT(c1) << 4) | HEX_TO_INT(c2);
	}

	haveTkt[index / 2] = 0;
	length = savedLength / 2;

	if (!CryptAcquireContext(
		&hProvider, NULL, MS_ENHANCED_PROV, PROV_RSA_FULL,
		CRYPT_VERIFYCONTEXT)) {
	    goto done;
	}

	if (!CryptCreateHash(hProvider, CALG_SHA1, 0, 0, &hHash)) {
	    goto done;
	}

	assert(harpyMagicSize == sizeof(harpyMagicValue));
	assert(harpyMagicSize == sizeof(DWORD64));
	assert(sizeof(tktKey) == sizeof(DWORD64));

	memcpy(tktKey, harpyMagicValue, sizeof(tktKey));

	mix = MAYBE_MUTATE(processId);

	for (index = 0; index < sizeof(tktKey); index++)
	    tktKey[index] = OBFUSCATE(harpyMagicValue, index, mix);

	if (!CryptHashData(hHash, tktKey, sizeof(tktKey), 0)) {
	    goto done;
	}

	if (!CryptDeriveKey(hProvider, CALG_RC4, hHash, 0, &hKey)) {
	    goto done;
	}

	assert(length >= 0);
	decryptLength = length;

	if (!CryptDecrypt(
		hKey, 0, TRUE, 0, (LPBYTE)haveTkt, &decryptLength)) {
	    goto done;
	}

	assert(decryptLength == (DWORD)length); /* RC4 same length */

	memset(wantTkt, 0, sizeof(wantTkt));

	wsprintfA(wantTkt, LICENSE_TICKET_FORMAT, processId);

	memset(wantTktId, 0, sizeof(wantTktId));

	if (GetEnvironmentVariableA(
		TICKET_ID_ENVVAR_NAME, wantTktId, TKT_ID_BUF)) {
	    if (lstrlenA(wantTktId) == TICKET_ID_LENGTH) {
		lstrcatA(wantTkt, " ");
		lstrcatA(wantTkt, wantTktId);
	    } else {
		goto done;
	    }
	}

#if defined(HARPY_TEST_EXE)
	fprintf(stderr, "have ticket \"%s\", want ticket \"%s\"\n",
	    haveTkt, wantTkt);
#endif /* defined(HARPY_TEST_EXE) */

	if (lstrcmpA(haveTkt, wantTkt) == 0) {
	    bResult = TRUE;
	}
    }

done:

    if (hKey) {
	HARPY_VVA_ONLY(bCleanup = )CryptDestroyKey(hKey);
	assert(bCleanup && "CryptDestroyKey");
	hKey = 0;
    }

    if (hHash) {
	HARPY_VVA_ONLY(bCleanup = )CryptDestroyHash(hHash);
	assert(bCleanup && "CryptDestroyHash");
	hHash = 0;
    }

    if (hProvider) {
	HARPY_VVA_ONLY(bCleanup = )CryptReleaseContext(hProvider, 0);
	assert(bCleanup && "CryptReleaseContext");
	hProvider = 0;
    }

    memset(wantTktId, 0, sizeof(wantTktId));
    memset(wantTkt, 0, sizeof(wantTkt));
    memset(haveTkt, 0, sizeof(haveTkt));

    return bResult;
}

/*
 *----------------------------------------------------------------------
 *
 * VerifyIsLicensed --
 *
 *	This function attempts to verify licensing for this library
 *	using the specified managed assembly.
 *
 * Results:
 *	S_OK if this library is properly licensed -OR- an HRESULT
 *	indicating a reason for the failure.
 *
 * Side effects:
 *	One or more managed assemblies may be loaded, there may be
 *	arbitrary side-effects.
 *
 *----------------------------------------------------------------------
 */

HARPY_API HRESULT VerifyIsLicensed(
    BOOL bUseMinimumClr,		/* Force using the minimum
					 * supported CLR version? */
    ICLRRuntimeHost *pClrRuntimeHost)	/* Handle to the running CLR
					 * instance. */
{
    HRESULT hResult;
    HMODULE hThisModule = hHarpyModule;
    HMODULE hClrModule;
    HANDLE hHeap = NULL;
    LPWSTR path = NULL;
    LPCWSTR assemblyName;
    BOOL bAssemblyNameOnly = FALSE;
    DWORD size;
    DWORD envSize;
    BOOLEAN wasVerified = 0;
    LPBYTE pStrongNameToken = NULL;
    ULONG nStrongNameToken = 0;
    BYTE myStrongNameToken[] = MANAGED_STRONG_NAME_TOKEN;
    WCHAR envName[MAX_STR_BUF + 1];
    WCHAR envValue[MAX_STR_BUF + 1];

#if defined(USE_CLR_40)
    CLRCreateInstanceFnPtr pClrCreateInstance;
    ICLRMetaHost *pClrMetaHost = NULL;
    ICLRRuntimeInfo *pClrRuntimeInfo = NULL;
    ICLRStrongName *pClrStrongName = NULL;
    LPCWSTR clrVersion;
    BOOL bLoadable = FALSE;
#endif /* defined(USE_CLR_40) */

#if defined(USE_CORE_CLR)
    BOOL bCoreClr = FALSE;
#endif /* defined(USE_CORE_CLR) */

    DWORD returnValue;

    if (pClrRuntimeHost == NULL) {
	MaybeOutputString("invalid ICLRRuntimeHost.\n");
	hResult = HRESULT_FROM_WIN32(ERROR_INVALID_PARAMETER);
	goto done;
    }

    hHeap = GetProcessHeap();

    if (hHeap == NULL) {
	MaybeOutputString("invalid process heap.\n");
	hResult = HRESULT_FROM_WIN32(GetLastError());
	goto done;
    }

#if !defined(_DEBUG) || defined(HARPY_SELF_TEST)
    if ((hThisModule != NULL) &&
	    CheckIsTrusted(hHeap, NULL, hThisModule)) {
	MaybeOutputString("module path not trusted.\n");
	hResult = CRYPT_E_NO_TRUSTED_SIGNER;
	goto done;
    }
#endif /* !defined(_DEBUG) || defined(HARPY_SELF_TEST) */

    assemblyName = UNICODE_TEXT(STRINGIFY(MANAGED_ASSEMBLY_NAME));

    size = UNICODE_STRING_MAX_CHARS;
    size += lstrlenW(assemblyName);
    size += 6; /* "<path>\<assemblyName>.dll\0" */

    path = HeapAlloc(
	hHeap, 0, (size + 1) * sizeof(WCHAR));

    if (path == NULL) {
	MaybeOutputString("could not allocate path.\n");
	hResult = E_OUTOFMEMORY;
	goto done;
    }

    memset(path, 0, (size + 1) * sizeof(WCHAR));

    envSize = GetEnvironmentVariableW(
	UNICODE_TEXT(ASSEMBLY_PATH_ENVVAR_NAME), path,
	UNICODE_STRING_MAX_CHARS);

    if ((envSize > 0) && (envSize <= UNICODE_STRING_MAX_CHARS)) {
	MaybeOutputString("assembly path env success.\n");
	bAssemblyNameOnly = TRUE;
	goto assemblyNameOnly;
    } else if (envSize == 0) {
	DWORD lastError = GetLastError();

	if (lastError == ERROR_ENVVAR_NOT_FOUND) {
	    MaybeOutputString("assembly path env not found.\n");
	} else {
	    MaybeOutputString("assembly path env failure.\n");
	    hResult = HRESULT_FROM_WIN32(lastError);
	    goto done;
	}
    } else {
	MaybeOutputString("bad assembly path env size.\n");
	hResult = ERROR_BUFFER_OVERFLOW;
	goto done;
    }

getModule:

    memset(path, 0, (size + 1) * sizeof(WCHAR));

    size = GetModuleFileNameW(
	hThisModule, path, UNICODE_STRING_MAX_CHARS);

    if (size == 0) {
	MaybeOutputString("could not get module file name.\n");
	hResult = HRESULT_FROM_WIN32(GetLastError());
	goto done;
    }

    if (!MaybeRemoveTailName(path, NULL)) {
	MaybeOutputString("could not trim module file name.\n");
	hResult = HRESULT_FROM_WIN32(ERROR_DIRECTORY);
	goto done;
    }

    /******************************************************************\
    |*           APPLICATION NATIVE DEPLOYMENT DIRECTORIES            *|
    \******************************************************************/

    MaybeRemoveTailName(path, L"x86");
    MaybeRemoveTailName(path, L"x64");
    MaybeRemoveTailName(path, L"ARM");
    MaybeRemoveTailName(path, L"ARM64");

    /******************************************************************\
    |*                NATIVE BUILD OUTPUT DIRECTORIES                 *|
    \******************************************************************/

    MaybeRemoveTailName(path, L"Win32");

    /******************************************************************\
    |*                NATIVE NUGET RUNTIME DIRECTORIES                *|
    \******************************************************************/

    MaybeRemoveTailName(path, L"native");

    /******************************************************************\
    |*              NUGET RUNTIME IDENTIFIER DIRECTORIES              *|
    \******************************************************************/

    MaybeRemoveTailName(path, L"linux-x64");
    MaybeRemoveTailName(path, L"linux-x86");
    MaybeRemoveTailName(path, L"linux-arm64");
    MaybeRemoveTailName(path, L"osx-x64");
    MaybeRemoveTailName(path, L"osx-arm64");
    MaybeRemoveTailName(path, L"osx");
    MaybeRemoveTailName(path, L"win-x64");
    MaybeRemoveTailName(path, L"win-x86");
    MaybeRemoveTailName(path, L"win-arm64");

    /******************************************************************\
    |*                   NUGET RUNTIME DIRECTORIES                    *|
    \******************************************************************/

    MaybeRemoveTailName(path, L"runtimes");

assemblyNameOnly:

    lstrcatW(path, L"\\");
    lstrcatW(path, assemblyName);
    lstrcatW(path, L".dll");

    if (GetFileAttributesW(path) == INVALID_FILE_ATTRIBUTES) {
	if (hThisModule == NULL) {
	    MaybeOutputString("assembly path not found via process.\n");
	    hResult = HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND);
	    goto done;
	} else {
	    MaybeOutputString("assembly path not found via module.\n");
	    hThisModule = NULL;
	    goto getModule;
	}
    }

    if (!bAssemblyNameOnly) {
	if (hThisModule == NULL) {
	    MaybeOutputString("assembly path found via process.\n");
	} else {
	    MaybeOutputString("assembly path found via module.\n");
	}
    }

#if !defined(_DEBUG) || defined(HARPY_SELF_TEST)
    if (CheckIsTrusted(hHeap, path, hThisModule)) {
	MaybeOutputString("assembly path not trusted.\n");
	hResult = TRUST_E_SUBJECT_NOT_TRUSTED;
	goto done;
    } else {
	MaybeOutputString("assembly path is trusted.\n");
    }
#endif /* !defined(_DEBUG) || defined(HARPY_SELF_TEST) */

    hClrModule = GetModuleHandleW(UNICODE_TEXT(CORE_CLR_MODULE_NAME));

    if (hClrModule != NULL) {
	MaybeOutputString("detected .NET Core in process.\n");

#if defined(USE_CORE_CLR)
	bCoreClr = TRUE;
#endif /* defined(USE_CORE_CLR) */

	goto skipToken; /* Unsupported on .NET Core. */
    }

#if defined(USE_CLR_40)
    hClrModule = GetModuleHandleW(UNICODE_TEXT(CLR_MODULE_NAME));

    if (hClrModule == NULL) {
	MaybeOutputString("missing CLR module in process.\n");
	hResult = HRESULT_FROM_WIN32(GetLastError());
	goto done;
    }

    pClrCreateInstance = (CLRCreateInstanceFnPtr)GetProcAddress(
	hClrModule, CLR_PROC_NAME);

    if (pClrCreateInstance == NULL) {
	MaybeOutputString("missing CLR function.\n");
#if defined(HARPY_STRONGNAME_CLR_20)
	goto fallback; /* Missing CLRv4? */
#else /* defined(HARPY_STRONGNAME_CLR_20) */
	hResult = HRESULT_FROM_WIN32(ERROR_FUNCTION_NOT_CALLED);
	goto done;
#endif /* defined(HARPY_STRONGNAME_CLR_20) */
    }

    hResult = pClrCreateInstance(
	&CLSID_CLRMetaHost, &IID_ICLRMetaHost,
	&pClrMetaHost);

    if (FAILED(hResult)) {
	MaybeOutputString("could not create ICLRMetaHost.\n");
	assert(pClrMetaHost == NULL);

	if (hResult == E_NOTIMPL) {
	    MaybeOutputString("CLR creation not implemented.\n");
#if defined(HARPY_STRONGNAME_CLR_20)
	    goto fallback; /* Missing CLRv4? */
#else /* defined(HARPY_STRONGNAME_CLR_20) */
	    goto done;
#endif /* defined(HARPY_STRONGNAME_CLR_20) */
	}

	goto done;
    }

    clrVersion = bUseMinimumClr ?
	UNICODE_TEXT(CLR_VERSION_MINIMUM) :
	UNICODE_TEXT(CLR_VERSION_LATEST);

    hResult = ICLRMetaHost_GetRuntime(
	pClrMetaHost, clrVersion, &IID_ICLRRuntimeInfo,
	&pClrRuntimeInfo);

    if (FAILED(hResult)) {
	MaybeOutputString("could not get ICLRRuntimeInfo.\n");
	assert(pClrRuntimeInfo == NULL);
	goto done;
    }

    hResult = ICLRRuntimeInfo_IsLoadable(
	pClrRuntimeInfo, &bLoadable);

    if (FAILED(hResult)) {
	MaybeOutputString("ICLRRuntimeInfo loadable failure.\n");
	goto done;
    }

    if (!bLoadable) {
	MaybeOutputString("ICLRRuntimeInfo not loadable.\n");
	goto done;
    }

    hResult = ICLRRuntimeInfo_GetInterface(
	pClrRuntimeInfo, &CLSID_CLRStrongName,
	&IID_ICLRStrongName, &pClrStrongName);

    if (FAILED(hResult)) {
	MaybeOutputString("could not get ICLRStrongName.\n");
	assert(pClrStrongName == NULL);
	goto done;
    }

    hResult = ICLRStrongName_StrongNameSignatureVerificationEx(
	pClrStrongName, path, 1, &wasVerified);

    if (FAILED(hResult)) {
	MaybeOutputString("modern strong name check failure.\n");
	goto done;
    }

    if (!wasVerified) {
	MaybeOutputString("modern strong name check unverified.\n");
	hResult = FUSION_E_SIGNATURE_CHECK_FAILED;
	goto done;
    }

    hResult = ICLRStrongName_StrongNameTokenFromAssembly(
	pClrStrongName, path, &pStrongNameToken, &nStrongNameToken);

    if (FAILED(hResult)) {
	MaybeOutputString("modern strong name token failure.\n");
	goto done;
    }

    MaybeOutputString("modern strong name check verified.\n");
    goto checkToken;

#if defined(HARPY_STRONGNAME_CLR_20)
fallback:
#endif /* defined(HARPY_STRONGNAME_CLR_20) */
#endif /* defined(USE_CLR_40) */

#if defined(HARPY_STRONGNAME_CLR_20)
    if (!StrongNameSignatureVerificationEx(path, 1, &wasVerified)) {
	MaybeOutputString("legacy strong name check failure.\n");
	hResult = FUSION_E_SIGNATURE_CHECK_FAILED;
	goto done;
    }

    if (!wasVerified) {
	MaybeOutputString("legacy strong name check unverified.\n");
	hResult = FUSION_E_SIGNATURE_CHECK_FAILED;
	goto done;
    }

    if (!StrongNameTokenFromAssembly(
	    path, &pStrongNameToken, &nStrongNameToken)) {
	MaybeOutputString("legacy strong name token failure.\n");
	hResult = StrongNameErrorInfo();
	goto done;
    }

    MaybeOutputString("legacy strong name check verified.\n");
#endif /* defined(HARPY_STRONGNAME_CLR_20) */

#if defined(USE_CLR_40)
checkToken:
#endif /* defined(USE_CLR_40) */

    if (!wasVerified) {
	MaybeOutputString("strong name check was not verified.\n");
	hResult = HRESULT_FROM_WIN32(ERROR_INVALID_VERIFY_SWITCH);
	goto done;
    }

    if ((nStrongNameToken == 0) || (pStrongNameToken == NULL)) {
	MaybeOutputString("strong name token data missing.\n");
	hResult = HRESULT_FROM_WIN32(ERROR_NO_DATA_DETECTED);
	goto done;
    }

    if (nStrongNameToken != sizeof(myStrongNameToken)) {
	MaybeOutputString("strong name token size mismatch.\n");
	hResult = HRESULT_FROM_WIN32(ERROR_BAD_LENGTH);
	goto done;
    }

    if (memcmp(pStrongNameToken,
	    myStrongNameToken, nStrongNameToken) != 0) {
	MaybeOutputString("strong name token data mismatch.\n");
	hResult = TYPE_E_TYPEMISMATCH;
	goto done;
    }

    MaybeOutputString("strong name size and data matched.\n");

skipToken:

    if (TryToValidateProcessLicenseTicket()) {
	MaybeOutputString("process license ticket validated.\n");
	hResult = S_OK;
	goto done;
    }

#if defined(USE_CORE_CLR)
    if (bCoreClr) goto useDefAppDomain;
#endif /* defined(USE_CORE_CLR) */

    memset(envValue, 0, sizeof(envValue));

    if (GetEnvironmentVariableW(
	    UNICODE_TEXT(OTHER_APPDOMAIN_ENVVAR_NAME), envValue,
	    MAX_STR_BUF)) {
	DWORD appDomainId = 0;
	FExecuteInAppDomainCallback pCallback = NULL;

	MaybeOutputString("using non-default application domain.\n");

	hResult = ICLRRuntimeHost_GetCurrentAppDomainId(
	    pClrRuntimeHost, &appDomainId);

	if (FAILED(hResult)) {
	    MaybeOutputString("no current application domain?\n");
	    goto done;
	}

	memset(envName, 0, sizeof(envName));

	wsprintfW(envName, UNICODE_TEXT(CALLBACK_ENVVAR_FORMAT),
	    GetCurrentProcessId(), appDomainId, GetCurrentThreadId());

	memset(envValue, 0, sizeof(envValue));

	if (!GetEnvironmentVariableW(envName, envValue, MAX_STR_BUF)) {
	    MaybeOutputString("could not get setup method callback.\n");
	    hResult = HRESULT_FROM_WIN32(GetLastError());
	    goto done;
	}

	if (!SetEnvironmentVariableW(envName, NULL)) { /* DELETE */
	    MaybeOutputString("could not unset setup method callback.\n");
	    hResult = HRESULT_FROM_WIN32(GetLastError());
	    goto done;
	}

	errno = ENOERR;
	pCallback = (FExecuteInAppDomainCallback)(intptr_t)_wtoi64(envValue);

	if ((pCallback != NULL) && (errno == ENOERR)) {
	    MaybeOutputString("good callback from setup method.\n");
	} else {
	    MaybeOutputString("bad callback from setup method.\n");
	    hResult = HRESULT_FROM_WIN32(ERROR_CALLBACK_SUPPLIED_INVALID_DATA);
	    goto done;
	}

	returnValue = HARPY_VERIFY_FAILURE;

	hResult = ICLRRuntimeHost_ExecuteInAppDomain(
	    pClrRuntimeHost, appDomainId, pCallback, &returnValue);
    } else {
#if defined(USE_CORE_CLR)
useDefAppDomain:
#endif /* defined(USE_CORE_CLR) */
	MaybeOutputString("using default application domain.\n");

	returnValue = HARPY_VERIFY_FAILURE;

	hResult = ICLRRuntimeHost_ExecuteInDefaultAppDomain(
	    pClrRuntimeHost, path,
	    UNICODE_TEXT(STRINGIFY(MANAGED_TYPE_NAME)),
	    UNICODE_TEXT(STRINGIFY(MANAGED_METHOD_NAME)),
	    NULL, &returnValue);
    }

    if (SUCCEEDED(hResult)) {
	if (returnValue == HARPY_VERIFY_SUCCESS) {
	    MaybeOutputString("verify method returned success.\n");
	} else if (returnValue == HARPY_VERIFY_QUEUED) {
	    MaybeOutputString("verify method returned queued.\n");
	} else {
	    MaybeOutputString("verify method returned failure.\n");
	    hResult = CLASS_E_NOTLICENSED;
	}
#if defined(USE_CORE_CLR)
    } else if (bCoreClr && (hResult == HOST_E_INVALIDOPERATION)) {
	/*
	 * HACK: All versions of the .NET Core runtime prior to 3.x
	 *       always return HOST_E_INVALIDOPERATION (0x80131022)
	 *       for the ICLRRuntimeHost_ExecuteInDefaultAppDomain
	 *       method; therefore, assume that we are running one
	 *       of those versions and do nothing.  Please see the
	 *       CoreCLR source code file "src/vm/corhost.cpp" for
	 *       more details.
	 */
	MaybeOutputString("verify method unreachable.\n");
	hResult = S_OK;
#endif /* defined(USE_CORE_CLR) */
    } else {
	MaybeOutputString("could not execute verify method.\n");
    }

done:

#if defined(USE_CLR_40)
    if (pClrStrongName != NULL) {
	if (pStrongNameToken != NULL) {
	    HRESULT hFreeResult = ICLRStrongName_StrongNameFreeBuffer(
		pClrStrongName, pStrongNameToken);

	    pStrongNameToken = NULL;

	    if (FAILED(hFreeResult)) {
		MaybeOutputString("could not free strong name buffer.\n");
		hResult = HRESULT_FROM_WIN32(hFreeResult);
	    }
	}

	ICLRStrongName_Release(pClrStrongName);
	pClrStrongName = NULL;
    }

    if (pClrRuntimeInfo != NULL) {
	ICLRRuntimeInfo_Release(pClrRuntimeInfo);
	pClrRuntimeInfo = NULL;
    }

    if (pClrMetaHost != NULL) {
	ICLRMetaHost_Release(pClrMetaHost);
	pClrMetaHost = NULL;
    }
#endif /* defined(USE_CLR_40) */

#if defined(HARPY_STRONGNAME_CLR_20)
    if (pStrongNameToken != NULL) {
	StrongNameFreeBuffer(pStrongNameToken);
	pStrongNameToken = NULL;
    }
#endif /* defined(HARPY_STRONGNAME_CLR_20) */

    if (path != NULL) {
	assert(hHeap != NULL);
	HeapFree(hHeap, 0, path);
	path = NULL;
    }

    MaybeOutputHResult(hResult);
    return hResult;
}

/*
 *----------------------------------------------------------------------
 *
 * VerifyClrCleanup --
 *
 *	This function cleans up the ICLRRuntimeHost interface pointer
 *	that is passed into it.
 *
 * Results:
 *	S_OK if the CLR is cleaned up for the current operation -OR-
 *	an HRESULT indicating a reason for the failure.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

HARPY_API HRESULT VerifyClrCleanup(
    ICLRRuntimeHost **ppClrRuntimeHost)	/* IN, OUT: Handle to the running
					 * CLR instance. */
{
    HRESULT hResult = S_OK;
    ICLRRuntimeHost *pClrRuntimeHost;

    if (ppClrRuntimeHost == NULL) {
	MaybeOutputString("invalid ICLRRuntimeHost pointer.\n");
	hResult = E_POINTER;
	goto done;
    }

    pClrRuntimeHost = *ppClrRuntimeHost;

    if (pClrRuntimeHost == NULL) {
	MaybeOutputString("invalid ICLRRuntimeHost.\n");
	hResult = HRESULT_FROM_WIN32(ERROR_INVALID_PARAMETER);
	goto done;
    }

#if defined(HARPY_SELF_TEST) && (defined(HARPY_EXE) || defined(HARPY_TEST_EXE))
    hResult = ICLRRuntimeHost_Stop(pClrRuntimeHost);

    if (SUCCEEDED(hResult)) {
	MaybeOutputString("ICLRRuntimeHost stop success.\n");
    } else {
	MaybeOutputString("ICLRRuntimeHost stop failure.\n");
	goto done;
    }
#endif /* defined(HARPY_SELF_TEST) && (defined(HARPY_EXE) || defined(HARPY_TEST_EXE)) */

    ICLRRuntimeHost_Release(pClrRuntimeHost);
    pClrRuntimeHost = NULL;

    *ppClrRuntimeHost = pClrRuntimeHost;
    MaybeOutputString("done with cleanup.\n");

done:

    MaybeOutputHResult(hResult);
    return hResult;
}

/*
 *----------------------------------------------------------------------
 *
 * DllMain --
 *
 *	This routine is called by the CRT library initialization
 *	code, or the DllEntryPoint routine.  It is responsible for
 *	initializing various dynamically loaded libraries.  Nothing
 *	overly complex or creative should be done in this function
 *	because the loader lock is held while it is executing (i.e.
 *	we cannot do anything that would cause another DLL to be
 *	loaded or unloaded, either directly or indirectly).
 *
 * Results:
 *	TRUE on success, FALSE on failure.  The result is ignored by
 *	Windows unless the reason is DLL_PROCESS_ATTACH.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

BOOL WINAPI DllMain(
    HINSTANCE hInstance,	/* The handle to the DLL module.  The
				 * value is the base address of the
				 * DLL.  The HINSTANCE of a DLL is the
				 * same as the HMODULE of the DLL, so
				 * it can be used in calls to functions
				 * that require a module handle. */
    DWORD reason,		/* The reason code that indicates why
				 * the DLL entry-point function is
				 * being called. */
    LPVOID reserved)		/* If reason is DLL_PROCESS_ATTACH,
				 * reserved is NULL for dynamic loads
				 * and non-NULL for static loads.  If
				 * reason is DLL_PROCESS_DETACH,
				 * reserved is NULL if FreeLibrary has
				 * been called or the DLL load failed
				 * and non-NULL if the process is
				 * terminating. */
{
    switch (reason) {
	case DLL_PROCESS_ATTACH: {
	    /*
	     * NOTE: This library does not handle DLL_THREAD_ATTACH
	     *       and DLL_THREAD_DETACH notifications.
	     */

	    DisableThreadLibraryCalls(hInstance);

	    /*
	     * NOTE: Save the package module handle for later usage.
	     */

	    hHarpyModule = hInstance;
	    break;
	}
    }

    return TRUE;
}

#if defined(HARPY_SELF_TEST)
/*
 *----------------------------------------------------------------------
 *
 * VerifySelfTest --
 *
 *	This function is used to test functionality provided by this
 *	module.  It is only included when the this module is compiled
 *	with HARPY_SELF_TEST defined.
 *
 * Results:
 *	Zero for success, non-zero for failure.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

HARPY_API HRESULT VerifySelfTest(VOID)
{
#include "entry-point.c"

    return S_OK;
}

#if defined(HARPY_TEST_EXE)
/*
 *----------------------------------------------------------------------
 *
 * wmain --
 *
 *	This function is used to test functionality provided by this
 *	module.  It is only included when the this module is compiled
 *	with HARPY_SELF_TEST and HARPY_TEST_EXE defined.
 *
 * Results:
 *	Zero for success, non-zero for failure.
 *
 * Side effects:
 *	None.
 *
 *----------------------------------------------------------------------
 */

int wmain(
    int argc,
    wchar_t *argv[])
{
    HRESULT hInfoResult;
    CONST LPCSTR *azStrings = NULL;
    CHAR chkBreak[MAX_STR_BUF + 1];

    if (GetEnvironmentVariableA("BREAK", chkBreak, MAX_STR_BUF)) {
	fprintf(stderr,
	    "attach debugger to process %d and press any key to continue.\n",
	    GetCurrentProcessId());
	fgetc(stdin);
	DebugBreak();
    }

    hInfoResult = GetSdkInfoStrings(&azStrings);

    if (SUCCEEDED(hInfoResult)) {
	int index = 0;
	fprintf(stdout, "INFO SUCCESS:");
	while (azStrings[index]) {
	    fprintf(stdout, " %s", azStrings[index]);
	    index++;
	}
	fprintf(stdout, "\n");
    } else {
	fprintf(stdout, "INFO FAILURE: ");
	MaybeOutputHResult(hInfoResult);
    }

#include "entry-point.c"

    fprintf(stdout, "SUCCESS\n");
    return 0;
}
#endif /* defined(HARPY_TEST_EXE) */
#endif /* defined(HARPY_SELF_TEST) */
#endif /* defined(_WIN32) */
