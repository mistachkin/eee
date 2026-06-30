/*
 * Object.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System.Collections.Generic;
using System.Reflection;
using Eagle._Attributes;
using Licensing.Components.Private;

namespace Licensing.Comparers
{
    /// <summary>
    /// Provides equality comparison for arbitrary objects, with specialized
    /// handling for byte arrays (compared by element) and strings (compared
    /// by value).  Other objects fall back to the default equality
    /// comparer.
    /// </summary>
    [ObjectId("abeb4a17-5769-43f4-9535-44a11401e65d")]
    internal sealed class Object : IEqualityComparer<object>
    {
        #region Public Constructors
        /// <summary>
        /// Constructs a new instance of the <see cref="Object" /> equality
        /// comparer.
        /// </summary>
        public Object()
        {
            // do nothing.
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region IEqualityComparer<object> Members
        /// <summary>
        /// Determines whether two objects are considered equal.  Byte arrays
        /// are compared by length and by each element; strings are compared
        /// by value; all other objects use the default equality comparer.
        /// </summary>
        /// <param name="left">
        /// The first object to compare.
        /// </param>
        /// <param name="right">
        /// The second object to compare.
        /// </param>
        /// <returns>
        /// Non-zero if the two objects are considered equal; otherwise,
        /// zero.
        /// </returns>
#if OBFUSCATION
        //
        // HACK: Workaround for Crypto Obfuscator for .Net, to prevent
        //       errors like "Method 'X' in type 'Y' from assembly 'Z'
        //       does not have an implementation." errors.
        //
        [Obfuscation(Feature = "renaming")]
#endif
        public new bool Equals(
            object left, /* in */
            object right /* in */
            )
        {
            if ((left is byte[]) && (right is byte[]))
            {
                byte[] arrayLeft = (byte[])left;
                byte[] arrayRight = (byte[])right;

                if (arrayLeft.Length != arrayRight.Length)
                    return false;

                for (int index = 0; index < arrayLeft.Length; index++)
                    if (arrayLeft[index] != arrayRight[index])
                        return false;

                return true;
            }
            else if ((left is string) && (right is string))
            {
                return left.Equals(right);
            }
            else
            {
                return EqualityComparer<object>.Default.Equals(left, right);
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a hash code for the specified object.  For byte arrays, a
        /// hash code is computed from the array length and each element;
        /// strings use their own hash code; all other objects use the
        /// default equality comparer.
        /// </summary>
        /// <param name="value">
        /// The object for which to compute a hash code.
        /// </param>
        /// <returns>
        /// A hash code for the specified object.
        /// </returns>
#if OBFUSCATION
        //
        // HACK: Workaround for Crypto Obfuscator for .Net, to prevent
        //       errors like "Method 'X' in type 'Y' from assembly 'Z'
        //       does not have an implementation." errors.
        //
        [Obfuscation(Feature = "renaming")]
#endif
        public int GetHashCode(
            object value /* in */
            )
        {
            if (value is byte[])
            {
                byte[] array = (byte[])value;
                int result = Constants.HashCodeMagic;

                result ^= array.Length;

                for (int index = 0; index < array.Length; index++)
                    result ^= array[index];

                return result;
            }
            else if (value is string)
            {
                return value.GetHashCode();
            }
            else
            {
                return EqualityComparer<object>.Default.GetHashCode(value);
            }
        }
        #endregion
    }
}
