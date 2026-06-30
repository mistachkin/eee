/*
 * HarpyRes.h --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#ifndef _HARPY_RES_H_
#define _HARPY_RES_H_

#if !defined(HARPY_SDK_NAME)
#  define HARPY_SDK_NAME			"Harpy SDK"
#endif

#if !defined(HARPY_SDK_VERSION)
#  define HARPY_SDK_VERSION			"v1.15"
#endif

#if defined(_MSC_VER) && _MSC_VER >= 1600 && defined(CLR_40)
#  define USE_CLR_40
#elif defined(RC_MSC_VER) && RC_MSC_VER >= 1600 && defined(CLR_40)
#  define USE_CLR_40
#endif

#if defined(USE_CLR_40)
#  if defined(USE_CORE_CLR)
#    if defined(HARPY_STRONGNAME_CLR_20)
#      define HARPY_USE_OPTIONS "CLRv4 CoreCLR LegacyStrongName"
#    else
#      define HARPY_USE_OPTIONS "CLRv4 CoreCLR"
#    endif
#  else
#    if defined(HARPY_STRONGNAME_CLR_20)
#      define HARPY_USE_OPTIONS "CLRv4 LegacyStrongName"
#    else
#      define HARPY_USE_OPTIONS "CLRv4"
#    endif
#  endif
#else
#  if defined(USE_CORE_CLR)
#    if defined(HARPY_STRONGNAME_CLR_20)
#      define HARPY_USE_OPTIONS "CLRv2 CoreCLR LegacyStrongName"
#    else
#      define HARPY_USE_OPTIONS "CLRv2 CoreCLR"
#    endif
#  else
#    if defined(HARPY_STRONGNAME_CLR_20)
#      define HARPY_USE_OPTIONS "CLRv2 LegacyStrongName"
#    else
#      define HARPY_USE_OPTIONS "CLRv2"
#    endif
#  endif
#endif

#if defined(_WIN32)
#  if !defined(LICENSE_TICKET_FORMAT)
#    define LICENSE_TICKET_FORMAT		\
			HARPY_SDK_NAME " " HARPY_SDK_VERSION \
			" License Ticket for Process %lu"
#  endif
#endif

#endif /* _HARPY_RES_H_ */
