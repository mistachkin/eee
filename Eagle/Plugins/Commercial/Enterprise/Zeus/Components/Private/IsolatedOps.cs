/*
 * IsolatedOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Globalization;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Eagle._Interfaces.Public;

#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
using _NewProcedureCallback =
    Eagle._Components.Public.Delegates.NewProcedureCallback;
#endif

namespace Zeus.Components.Private
{
    /// <summary>
    /// Provides helper methods that deal with application domain isolation for
    /// the Zeus plugin, including selecting the binder and the appropriate
    /// application domain for type resolution and provider creation, and
    /// installing or removing the new-procedure callback (bridging it across
    /// domain boundaries when the plugin is isolated).
    /// </summary>
    [ObjectId("45b2a2a3-4825-451d-a4d3-806bbb7dc94a")]
    internal static class IsolatedOps
    {
        #region Private Constants
        //
        // NOTE: This is the culture that will be returned when there is no
        //       interpreter available.
        //
        /// <summary>
        /// The culture used when no interpreter is available to supply one.
        /// </summary>
        private static readonly CultureInfo DefaultCultureInfo =
            CultureInfo.InvariantCulture;
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Plugin Helper Methods
        /// <summary>
        /// Gets the binder to use for the specified interpreter and plugin.
        /// Returns null when there is no interpreter, or when the plugin has
        /// been loaded into an application domain different from the
        /// interpreter (in which case the script binder cannot be used).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose binder is requested.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data used to determine whether the plugin is in a
        /// different application domain than the interpreter.
        /// </param>
        /// <returns>
        /// The interpreter's binder, or null when one cannot be used.
        /// </returns>
        public static IBinder GetBinder(
            Interpreter interpreter, /* in */
            IPluginData pluginData   /* in */
            )
        {
            //
            // BUGFIX: We cannot use the ScriptBinder if this plugin has been
            //         loaded into an AppDomain different from the interpreter
            //         -OR- there is no interpreter to obtain it from.
            //
            if (interpreter != null)
            {
                if (IsCrossAppDomain(interpreter, pluginData))
                    return null;

                return interpreter.Binder;
            }
            else
            {
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Installs or removes the interpreter's new-procedure callback for
        /// the specified plugin.  When installing and the plugin is in a
        /// different application domain, a cross-domain callback bridge is
        /// created (requiring isolated interpreter or plugin support); the
        /// callback is then set (or cleared) under an interpreter lock.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose new-procedure callback is being changed.
        /// </param>
        /// <param name="plugin">
        /// The plugin that supplies and owns the new-procedure callback.
        /// </param>
        /// <param name="install">
        /// Non-zero to install the callback; zero to remove it.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives an error message describing the problem.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise, another
        /// <see cref="ReturnCode" /> value that indicates the type of
        /// failure.
        /// </returns>
        public static ReturnCode InstallNewProcedureCallbacks(
            Interpreter interpreter, /* in */
            IPlugin plugin,          /* in */
            bool install,            /* in */
            ref Result error         /* out */
            )
        {
            if (interpreter == null)
            {
                error = "invalid interpreter";
                return ReturnCode.Error;
            }

#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
            NewProcedureCallbackBridge callbackBridge = null;
#endif

            if (install && Utility.IsCrossAppDomain(interpreter, plugin))
            {
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
                callbackBridge = NewProcedureCallbackBridge.Create(
                    new NewProcedureCallback(plugin), ref error);

                if (callbackBridge == null)
                    return ReturnCode.Error;
#else
                error = "cannot set delegates with plugin isolated";
                return ReturnCode.Error;
#endif
            }

            bool locked = false;

            try
            {
                interpreter.TryLockWithWait(
                    ref locked); /* TRANSACTIONAL */

                if (locked)
                {
                    if (install)
                    {
#if ISOLATED_INTERPRETERS || ISOLATED_PLUGINS
                        if (callbackBridge != null)
                        {
                            interpreter.NewProcedureCallback =
                                new _NewProcedureCallback(
                                    callbackBridge.NewProcedureCallback);
                        }
                        else
#endif
                        {
                            interpreter.NewProcedureCallback =
                                new NewProcedureCallback(
                                    plugin).NewProcedure;
                        }
                    }
                    else
                    {
                        interpreter.NewProcedureCallback = null;
                    }

                    return ReturnCode.Ok;
                }
                else
                {
                    error = "interpreter is locked";
                }
            }
            catch (Exception e)
            {
                error = e;
            }
            finally
            {
                interpreter.ExitLock(
                    ref locked); /* TRANSACTIONAL */
            }

            return ReturnCode.Error;
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Private Helper Methods
        /// <summary>
        /// Determines whether the specified plugin resides in a different
        /// application domain than the specified interpreter (or, when no
        /// interpreter is supplied, than the current domain).
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter to compare against, or null to compare against the
        /// current application domain.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose application domain is examined.
        /// </param>
        /// <returns>
        /// Non-zero if the plugin is in a different application domain;
        /// otherwise, zero.
        /// </returns>
        private static bool IsCrossAppDomain(
            Interpreter interpreter, /* in */
            IPluginData pluginData   /* in */
            )
        {
            if (interpreter != null)
                return Utility.IsCrossAppDomain(interpreter, pluginData);
            else
                return Utility.IsCrossAppDomain(pluginData);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Value / Type Helper Methods
        /// <summary>
        /// Gets the application domain in which a provider should be created
        /// for the specified interpreter and plugin.  When the plugin is in a
        /// different domain, the plugin's domain is used; otherwise the
        /// interpreter's domain is used, falling back to the current domain.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter whose application domain may be used.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data whose application domain may be used.
        /// </param>
        /// <returns>
        /// The application domain in which the provider should be created.
        /// </returns>
        public static AppDomain GetAppDomainForCreate(
            Interpreter interpreter, /* in */
            IPluginData pluginData   /* in */
            )
        {
            bool crossAppDomain = IsCrossAppDomain(interpreter, pluginData);

            if (crossAppDomain)
            {
                if (pluginData != null)
                    return pluginData.AppDomain;
            }
            else if (interpreter != null)
            {
                return interpreter.GetAppDomain();
            }

            return AppDomain.CurrentDomain;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Gets the application domain to use when resolving a type.  This
        /// implementation always returns null so that the default type
        /// resolution behavior is used.
        /// </summary>
        /// <param name="interpreter">
        /// The interpreter; not used by this implementation.
        /// </param>
        /// <param name="pluginData">
        /// The plugin data; not used by this implementation.
        /// </param>
        /// <returns>
        /// Always null.
        /// </returns>
        public static AppDomain GetAppDomainForGetType(
            Interpreter interpreter, /* in: NOT USED */
            IPluginData pluginData /* in: NOT USED */
            )
        {
            return null;
        }
        #endregion
    }
}
