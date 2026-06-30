/*
 * CertificateFileOps.cs --
 *
 * Copyright (c) 2007-2012 by Joe Mistachkin.  All rights reserved.
 *
 * See the file "license.terms" for information on usage and redistribution of
 * this file, and for a DISCLAIMER OF ALL WARRANTIES.
 *
 * RCS: @(#) $Id: $
 */

using System;
using System.Collections.Generic;
using System.IO;
using Eagle._Attributes;
using Eagle._Components.Public;
using Eagle._Containers.Public;
using Utility = Eagle._Components.Public.Utility;

namespace Licensing.Components.Private
{
    /// <summary>
    /// Provides helper methods for working with certificate files on disk,
    /// including enumerating file and directory names, deleting files and
    /// directories, and computing the backup and original names used when
    /// certificate files are backed up or rolled back.
    /// </summary>
    [ObjectId("80f79277-6756-46e4-8fa8-091ddad0f239")]
    internal static class CertificateFileOps
    {
        #region Private Methods
        /// <summary>
        /// Builds the file name prefix used to identify backup copies of
        /// certificate files for the specified identifier.
        /// </summary>
        /// <param name="id">
        /// The identifier used to construct the backup prefix.
        /// </param>
        /// <returns>
        /// The formatted backup prefix string.
        /// </returns>
        private static string GetBackupPrefix( /* CORE */
            Guid id /* in */
            )
        {
            return String.Format(Constants.BackupPrefixFormat, id);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the backup file name (without directory) for the
        /// specified file name by prepending the backup prefix associated
        /// with the given identifier.
        /// </summary>
        /// <param name="fileNameOnly">
        /// The original file name, without any directory component.
        /// </param>
        /// <param name="id">
        /// The identifier used to construct the backup prefix.
        /// </param>
        /// <returns>
        /// The backup file name, or null if <paramref name="fileNameOnly" />
        /// is null or empty.
        /// </returns>
        private static string GetBackupFileNameOnly( /* CORE */
            string fileNameOnly, /* in */
            Guid id              /* in */
            )
        {
            if (String.IsNullOrEmpty(fileNameOnly))
                return null;

            string prefix = GetBackupPrefix(id);

            if (String.IsNullOrEmpty(prefix))
                return fileNameOnly;

            return String.Format("{0}{1}", prefix, fileNameOnly);
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Computes the original file name (without directory) for the
        /// specified backup file name by stripping the backup prefix
        /// associated with the given identifier.
        /// </summary>
        /// <param name="fileNameOnly">
        /// The backup file name, without any directory component.
        /// </param>
        /// <param name="id">
        /// The identifier used to construct the backup prefix.
        /// </param>
        /// <returns>
        /// The original file name with the backup prefix removed, or the
        /// unchanged value when it is null, empty, or has no prefix.
        /// </returns>
        private static string GetOriginalFileNameOnly( /* CORE */
            string fileNameOnly, /* in */
            Guid id              /* in */
            )
        {
            if (String.IsNullOrEmpty(fileNameOnly))
                return fileNameOnly;

            string prefix = GetBackupPrefix(id);

            if (String.IsNullOrEmpty(prefix))
                return fileNameOnly;

            return fileNameOnly.Substring(prefix.Length);
        }
        #endregion

        ///////////////////////////////////////////////////////////////////////

        #region Public Methods
        /// <summary>
        /// Determines whether the specified path exists as either a directory
        /// or a file.
        /// </summary>
        /// <param name="path">
        /// The path to check for existence.
        /// </param>
        /// <returns>
        /// Non-zero if a directory or file exists at
        /// <paramref name="path" />; otherwise, zero.
        /// </returns>
        public static bool PathExists( /* CORE */
            string path /* in */
            )
        {
            if (String.IsNullOrEmpty(path))
                return false;

            if (Directory.Exists(path) || File.Exists(path))
                return true;

            return false;
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns a sorted list of the file names in the specified directory
        /// that match the given search pattern, optionally including
        /// subdirectories.
        /// </summary>
        /// <param name="directory">
        /// The directory to search for matching files.
        /// </param>
        /// <param name="searchPattern">
        /// The search pattern used to match file names.
        /// </param>
        /// <param name="recursive">
        /// Non-zero to search all subdirectories as well; zero to search only
        /// the top-level directory.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// A sorted list of matching file names, or null if an error was
        /// encountered.
        /// </returns>
        public static StringList GetFileNames( /* CORE */
            string directory,     /* in */
            string searchPattern, /* in */
            bool recursive,       /* in */
            ref Result error      /* out */
            )
        {
            try
            {
                SearchOption searchOption = recursive ?
                    SearchOption.AllDirectories :
                    SearchOption.TopDirectoryOnly;

                string[] fileNames = Directory.GetFiles(
                    directory, searchPattern, searchOption);

                if (fileNames != null)
                {
                    Array.Sort(fileNames); /* O(N) */

                    return new StringList(fileNames);
                }

                return new StringList();
            }
            catch (Exception e)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateFileOps).Name,
                    TracePriority.Highest);
#endif

                error = e;
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Returns the unique set of directory names that contain the
        /// specified file names.
        /// </summary>
        /// <param name="fileNames">
        /// The file names whose containing directories are collected.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// A list of the unique directory names, or null if an error was
        /// encountered.
        /// </returns>
        public static StringList GetDirectoryNames( /* CORE */
            IEnumerable<string> fileNames, /* in */
            ref Result error               /* out */
            )
        {
            try
            {
                if (fileNames == null)
                {
                    error = "invalid file names";
                    return null;
                }

                StringList directories = new StringList();

                foreach (string fileName in fileNames)
                {
                    if (String.IsNullOrEmpty(fileName))
                        continue;

                    string directory = Path.GetDirectoryName(
                        fileName);

                    if (String.IsNullOrEmpty(directory))
                        continue;

                    directories.Add(directory);
                }

                return Utility.GetUniqueElements(directories);
            }
            catch (Exception e)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateFileOps).Name,
                    TracePriority.Highest);
#endif

                error = e;
                return null;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Validates that the specified file can be backed up and computes
        /// the backup file name to use. The source file must exist and the
        /// computed backup target must not already exist.
        /// </summary>
        /// <param name="fileName">
        /// The existing file name that is to be backed up.
        /// </param>
        /// <param name="id">
        /// The identifier used to construct the backup prefix.
        /// </param>
        /// <param name="newFileName">
        /// Upon success, receives the full backup file name to use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode CheckBackupFileName( /* CORE */
            string fileName,        /* in */
            Guid id,                /* in */
            ref string newFileName, /* out */
            ref Result error        /* out */
            )
        {
            try
            {
                if (String.IsNullOrEmpty(fileName))
                {
                    error = "invalid existing file name";
                    return ReturnCode.Error;
                }

                if (!File.Exists(fileName))
                {
                    error = String.Format(
                        "cannot backup file {0}, source missing",
                        Utility.FormatWrapOrNull(fileName));

                    return ReturnCode.Error;
                }

                string directory = Path.GetDirectoryName(
                    fileName);

                if (String.IsNullOrEmpty(directory))
                {
                    error = "invalid existing directory";
                    return ReturnCode.Error;
                }

                if (!Directory.Exists(directory))
                {
                    error = String.Format(
                        "directory {0} does not exist",
                        Utility.FormatWrapOrNull(directory));

                    return ReturnCode.Error;
                }

                string fileNameOnly = Path.GetFileName(
                    fileName);

                if (String.IsNullOrEmpty(fileNameOnly))
                {
                    error = String.Format(
                        "invalid existing file {0} name only",
                        Utility.FormatWrapOrNull(fileName));

                    return ReturnCode.Error;
                }

                string localNewFileName = Path.Combine(
                    directory, GetBackupFileNameOnly(
                    fileNameOnly, id));

                if (PathExists(localNewFileName))
                {
                    error = String.Format(
                        "cannot backup file {0}, target exists",
                        Utility.FormatWrapOrNull(fileName));

                    return ReturnCode.Error;
                }

                newFileName = localNewFileName;
                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateFileOps).Name,
                    TracePriority.Highest);
#endif

                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Validates that the specified backup file can be rolled back and
        /// computes the original file name to use. The backup file must exist
        /// and the computed original target must not already exist.
        /// </summary>
        /// <param name="fileName">
        /// The existing backup file name that is to be rolled back.
        /// </param>
        /// <param name="id">
        /// The identifier used to construct the backup prefix.
        /// </param>
        /// <param name="newFileName">
        /// Upon success, receives the full original file name to use.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode CheckOriginalFileName( /* CORE */
            string fileName,        /* in */
            Guid id,                /* in */
            ref string newFileName, /* out */
            ref Result error        /* out */
            )
        {
            try
            {
                if (String.IsNullOrEmpty(fileName))
                {
                    error = "invalid backup file name";
                    return ReturnCode.Error;
                }

                if (!File.Exists(fileName))
                {
                    error = String.Format(
                        "cannot rollback file {0}, source missing",
                        Utility.FormatWrapOrNull(fileName));

                    return ReturnCode.Error;
                }

                string directory = Path.GetDirectoryName(
                    fileName);

                if (String.IsNullOrEmpty(directory))
                {
                    error = "invalid backup directory";
                    return ReturnCode.Error;
                }

                if (!Directory.Exists(directory))
                {
                    error = String.Format(
                        "directory {0} does not exist",
                        Utility.FormatWrapOrNull(directory));

                    return ReturnCode.Error;
                }

                string fileNameOnly = Path.GetFileName(
                    fileName);

                if (String.IsNullOrEmpty(fileNameOnly))
                {
                    error = String.Format(
                        "invalid backup file {0} name only",
                        Utility.FormatWrapOrNull(fileName));

                    return ReturnCode.Error;
                }

                string newFileNameOnly = GetOriginalFileNameOnly(
                    fileNameOnly, id);

                if (String.IsNullOrEmpty(newFileNameOnly))
                {
                    error = String.Format(
                        "invalid original file {0} name only",
                        Utility.FormatWrapOrNull(fileName));

                    return ReturnCode.Error;
                }

                string localNewFileName = Path.Combine(
                    directory, newFileNameOnly);

                if (PathExists(localNewFileName))
                {
                    error = String.Format(
                        "cannot rollback file {0}, target exists",
                        Utility.FormatWrapOrNull(fileName));

                    return ReturnCode.Error;
                }

                newFileName = localNewFileName;
                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateFileOps).Name,
                    TracePriority.Highest);
#endif

                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Deletes the specified file if it exists. A missing file is treated
        /// as success.
        /// </summary>
        /// <param name="fileName">
        /// The name of the file to delete.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode DeleteFile( /* CORE */
            string fileName, /* in */
            ref Result error /* out */
            )
        {
            if (String.IsNullOrEmpty(fileName))
            {
                error = "invalid delete file name";
                return ReturnCode.Error;
            }

            if (!File.Exists(fileName))
                return ReturnCode.Ok;

            ///////////////////////////////////////////////////////////////////

            try
            {
                File.Delete(fileName); /* throw */
                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateFileOps).Name,
                    TracePriority.Highest);
#endif

                error = e;
                return ReturnCode.Error;
            }
        }

        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        /// Deletes the specified directory if it exists, optionally including
        /// its contents. A missing directory, or a non-empty directory when
        /// deletion fails with an I/O error, is treated as success.
        /// </summary>
        /// <param name="directory">
        /// The directory to delete.
        /// </param>
        /// <param name="recursive">
        /// Non-zero to delete the directory and all of its contents; zero to
        /// delete only an empty directory.
        /// </param>
        /// <param name="error">
        /// Upon failure, receives information about the error that was
        /// encountered.
        /// </param>
        /// <returns>
        /// <see cref="ReturnCode.Ok" /> on success; otherwise,
        /// <see cref="ReturnCode.Error" />.
        /// </returns>
        public static ReturnCode DeleteDirectory( /* CORE */
            string directory, /* in */
            bool recursive,   /* in */
            ref Result error  /* out */
            )
        {
            if (String.IsNullOrEmpty(directory))
            {
                error = "invalid delete directory";
                return ReturnCode.Error;
            }

            if (!Directory.Exists(directory))
                return ReturnCode.Ok;

            ///////////////////////////////////////////////////////////////////

            try
            {
                Directory.Delete(directory, recursive); /* throw */
                return ReturnCode.Ok;
            }
            catch (IOException)
            {
                //
                // HACK: Ignore exceptions caused by the
                //       target directory not being empty
                //       because that is expected in some
                //       cases and is totally harmless.
                //
                return ReturnCode.Ok;
            }
            catch (Exception e)
            {
#if DEBUG || FORCE_TRACE
                CertificateTraceOps.DebugTrace(
                    e, typeof(CertificateFileOps).Name,
                    TracePriority.Highest);
#endif

                error = e;
                return ReturnCode.Error;
            }
        }
        #endregion
    }
}
