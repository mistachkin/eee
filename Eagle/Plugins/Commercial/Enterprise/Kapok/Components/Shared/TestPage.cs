/*
 * TestPage.cs --
 *
 * Extensible Adaptable Generalized Logic Engine (Eagle)
 * Eagle Enterprise Edition: Kapok SDK v1.0
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if !KAPOK
using System.Runtime.InteropServices;
#endif

#if KAPOK
using Eagle._Attributes;
#endif

namespace Kapok.Components.Shared
{
    /// <summary>
    /// This instanced page class is intended for diagnostic and test use
    /// only.  When invoked, it should simply produce an empty successful
    /// response.
    /// </summary>
#if KAPOK
    [ObjectId("d685d7f8-ad79-4d0d-af62-dedb97672bb1")]
#else
    [Guid("d685d7f8-ad79-4d0d-af62-dedb97672bb1")]
#endif
    public class TestPage : BasePage
    {
        // do nothing.
    }
}
