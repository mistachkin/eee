  /****************** Begin "Harpy Native SDK" Integration *******************/
#if defined(_WIN32)
  {
    HRESULT hResult;
    ICLRRuntimeHost *pClrRuntimeHost = NULL;
    hResult = VerifyClrIsLoaded(FALSE, &pClrRuntimeHost);
    if( SUCCEEDED(hResult) && hResult!=S_FALSE ){
      hResult = VerifyIsLicensed(FALSE, pClrRuntimeHost);
    }
    VerifyClrCleanup(&pClrRuntimeHost); /* IGNORED */
    if( FAILED(hResult) ){
      char buffer[MAX_PATH + 1] = {0};
      _snprintf(buffer, MAX_PATH,
                "API called without license: %ld (0x%lx)",
                hResult, hResult);
      OutputDebugStringA(buffer);
      AppendToLogFileA(buffer);
#if defined(HARPY_SELF_TEST) && defined(HARPY_TEST_EXE)
      fprintf(stdout, "FAILURE: %s\n", buffer);
#endif
#if defined(SQLITE_MISUSE)
      sqlite3_log(SQLITE_MISUSE, "%s", buffer);
#endif
#if defined(HARPY_DEBUG)
      return hResult;
#elif defined(SQLITE_MISUSE)
      return SQLITE_MISUSE;
#else
      return ERROR_REQ_NOT_ACCEP; /* 71 */
#endif
    }
  }
#endif
  /******************* End "Harpy Native SDK" Integration ********************/
