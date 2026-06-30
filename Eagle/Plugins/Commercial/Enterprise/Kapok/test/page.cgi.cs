/*
 * page.cgi.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

#if KAPOK
using Eagle._Attributes;
#else
using System.Runtime.InteropServices;
#endif

using Kapok.Components.Shared;

namespace Kapok.test
{
    /// <summary>
    /// Implements the test page used to verify basic page handling.
    /// </summary>
#if KAPOK
    [ObjectId("31fcd6af-e01a-44fb-81c7-ecdf1b486333")]
#else
    [Guid("31fcd6af-e01a-44fb-81c7-ecdf1b486333")]
#endif
    public partial class page : TestPage
    {
        // do nothing.
    }
}
