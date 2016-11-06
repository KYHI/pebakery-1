﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Diagnostics;

// P/invoke
using System.Runtime.InteropServices;
using Microsoft.Win32;
using Microsoft.Win32.Interop;
using System.Security;
using System.Runtime.ConstrainedExecution;
using System.ComponentModel;
using System.IO.MemoryMappedFiles;

// Cabinet.dll

namespace BakeryEngine
{
    public class UnsupportedEncodingException : Exception
    {
        public UnsupportedEncodingException() { }
        public UnsupportedEncodingException(string message) : base(message) { }
        public UnsupportedEncodingException(string message, Exception inner) : base(message, inner) { }
    }

    /// <summary>
    /// Contains static helper methods.
    /// </summary>
    public static class FileHelper
    {
        /// <summary>
        /// Count occurrences of strings.
        /// http://www.dotnetperls.com/string-occurrence
        /// </summary>
        public static int CountStringOccurrences(string text, string pattern)
        {
            // Loop through all instances of the string 'text'.
            int count = 0;
            int i = 0;
            while ((i = text.IndexOf(pattern, i)) != -1)
            {
                i += pattern.Length;
                count++;
            }
            return count;
        }

        /// <summary>
        /// Detect text file's encoding with BOM
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static Encoding DetectTextEncoding(string fileName)
        {
            byte[] bom = new byte[4];
            FileStream fs = null;

            fs = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.Read);
            fs.Read(bom, 0, bom.Length);
            fs.Close();

            if (bom[0] == 0xEF && bom[1] == 0xBB && bom[2] == 0xBF)
                return Encoding.UTF8;
            else if (bom[0] == 0xFF && bom[1] == 0xFE)
                return Encoding.Unicode;
            else if (bom[0] == 0xFE && bom[1] == 0xFF)
                return Encoding.BigEndianUnicode;
            return Encoding.Default;
        }

        /// <summary>
        /// Write Unicode BOM into text file stream
        /// </summary>
        /// <param name="fs"></param>
        /// <param name="encoding"></param>
        /// <returns></returns>
        public static FileStream WriteTextBOM(FileStream fs, Encoding encoding)
        {
            if (encoding == Encoding.UTF8)
            {
                byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
                fs.Write(bom, 0, bom.Length);
            }
            else if (encoding == Encoding.Unicode)
            {
                byte[] bom = new byte[] { 0xFF, 0xFE };
                fs.Write(bom, 0, bom.Length);
            }
            else if (encoding == Encoding.BigEndianUnicode)
            {
                byte[] bom = new byte[] { 0xFE, 0xFF };
                fs.Write(bom, 0, bom.Length);
            }
            else if (encoding != Encoding.Default)
            { // Unsupported Encoding
                throw new UnsupportedEncodingException(encoding.ToString() + " is not supported");
            }

            return fs;
        }

        /// <summary>
        /// Read full text from file, detecting encoding by BOM.
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static string ReadTextFile(string fileName)
        {
            FileStream fs = new FileStream(fileName, FileMode.Open, FileAccess.Read);
            char[] buffer = new char[fs.Length];
            StreamReader sr = new StreamReader(fs, DetectTextEncoding(fileName));
            sr.Read(buffer, 0, buffer.Length);
            sr.Close();
            fs.Close();
            return new string(buffer);
        }

        /// <summary>
        /// return compile date and time
        /// </summary>
        /// <remarks>
        /// Add to pre-build event : echo %date% %time% > "$(ProjectDir)\Resources\BuildDate.txt"
        /// Add to "Resources\BuildData.txt" as resources
        /// </remarks>
        /// <returns></returns>
        /// http://stackoverflow.com/questions/1600962/displaying-the-build-date
        public static DateTime GetBuildDate()
        {
            // Ex) 2016-08-30  7:10:00.25 
            // Ex) 2016-09-02  0:25:11.65 
            string[] rawBuildDateStr = Properties.Resources.BuildDate.Split(new char[] { ' ', '.', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            string buildDateStr = string.Format("{0} {1}", rawBuildDateStr[0], rawBuildDateStr[1]);
            DateTime buildDate;
            try
            {
                buildDate = DateTime.ParseExact(buildDateStr, "yyyy-MM-dd H:mm:ss", CultureInfo.InvariantCulture);
            }
            catch (FormatException)
            { // Format Error, just print 0001-01-01
                buildDate = new DateTime(1, 1, 1);
            }

            return buildDate;
        }

        /// <summary>
        /// Read program's version from assembly
        /// </summary>
        /// <returns></returns>
        public static Version GetProgramVersion()
        {
            // Assembly assembly = Assembly.GetExecutingAssembly();
            // FileVersionInfo fileVerInfo = FileVersionInfo.GetVersionInfo(assembly.Location);
            // return new Version(fileVerInfo.FileMajorPart, fileVerInfo.FileMinorPart, fileVerInfo.FileBuildPart, fileVerInfo.FilePrivatePart);
            return Assembly.GetExecutingAssembly().GetName().Version;
        }

        public static string GetProgramAbsolutePath()
        {
            return FileHelper.RemoveLastDirChar(AppDomain.CurrentDomain.BaseDirectory);
        }

        /// <summary>
        /// Remove last \ in the path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string RemoveLastDirChar(string path)
        {
            if (Path.GetDirectoryName(path) != null)
                path = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return path;
        }

        /// <summary>
        /// Extends Path.GetDirectoryName().
        /// If returned dir path is empty, change it to "."
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetDirNameEx(string path)
        {
            path = FileHelper.RemoveLastDirChar(path);
            string dirName = Path.GetDirectoryName(path);
            if (dirName == string.Empty)
                dirName = ".";
            return dirName;
        }

        /// <summary>
        /// Get Parent directory name, not full path.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static string GetParentDirName(string path)
        {
            string dirName = Path.GetDirectoryName(path);
            int idx = dirName.LastIndexOf(Path.DirectorySeparatorChar);
            if (idx != -1)
                dirName = dirName.Substring(idx + 1, dirName.Length - (idx + 1));
            else
                dirName = string.Empty;

            return dirName;
        }

        /// <summary>
        /// Create temp file and mark with temp attribute.
        /// </summary>
        /// <returns></returns>
        public static string CreateTempFile()
        {
            string path = Path.GetTempFileName();
            FileInfo fileInfo = new FileInfo(path);
            fileInfo.Attributes = FileAttributes.Temporary;

            return path;
        }

        /// <summary>
        /// Replace src with dest. 
        /// </summary>
        /// <param name="src"></param>
        /// <param name="dest"></param>
        public static void FileReplaceEx(string src, string dest)
        {
            try
            {
                // File.Copy removes ACL and ADS.
                // Instead, use File.Replace
                File.Replace(src, dest, null);
            }
            catch (IOException)
            {
                // However, File.Replace throws IOException if src and dest files are in different volume.
                // In this case, use File.Copy as fallback.
                File.Copy(src, dest, true);
                File.Delete(src);
            }
        }

        public static long GetFileSize(string srcFile)
        {
            FileInfo info = new FileInfo(srcFile);
            return info.Length;
        }

        public static bool FindByteSignature (string srcFile, byte[] signature, out long offset)
        {
            long size = FileHelper.GetFileSize(srcFile);

            MemoryMappedFile mmap = MemoryMappedFile.CreateFromFile(srcFile, FileMode.Open);
            MemoryMappedViewAccessor accessor = mmap.CreateViewAccessor();

            byte[] buffer = new byte[signature.Length];
            bool found = false;

            offset = 0;

            for (long i = 0; i < size - signature.Length; i++)
            {
                accessor.ReadArray(i, buffer, 0, buffer.Length);
                if (signature.SequenceEqual(buffer))
                {
                    found = true;
                    offset = i;
                    break;
                }
            }

            accessor.Dispose();
            mmap.Dispose();

            return found;
        }

        public static void CopyOffset(string srcFile, string destFile, long offset, long length)
        {
            long size = FileHelper.GetFileSize(srcFile);

            MemoryMappedFile mmap = MemoryMappedFile.CreateFromFile(srcFile, FileMode.Open);
            MemoryMappedViewAccessor accessor = mmap.CreateViewAccessor();

            FileStream stream = new FileStream(destFile, FileMode.Create, FileAccess.Write);

            const int block = 4096; // Memory Page is 4KB!
            byte[] buffer = new byte[block];
            for (long i = offset - (offset % block); i < offset + length; i += block)
            {
                if (i == offset - (offset % block)) // First block
                {
                    accessor.ReadArray(i, buffer, 0, block);
                    stream.Write(buffer, (int) (offset % block), block - (int) (offset % block));
                }
                else if (offset + length - block <= i) // Last block // i < offset + length + block - ((offset + length) % block)
                {
                    accessor.ReadArray(i, buffer, 0, (int)((offset + length) % block));
                    stream.Write(buffer, 0, (int) ((offset + length) % block));
                }
                else // Middle. Just copy whole block
                {
                    accessor.ReadArray(i, buffer, 0, block);
                    stream.Write(buffer, 0, block);
                }
            }

            stream.Close();
            accessor.Dispose();
            mmap.Dispose();
        }

        /// <summary>
        /// Delete directory, handling open of the handle of the files
        /// </summary>
        /// <remarks>
        /// http://stackoverflow.com/questions/329355/cannot-delete-directory-with-directory-deletepath-true
        /// </remarks>
        /// <param name="path"></param>
        /// <param name="recursive"></param>
        public static void DirectoryDeleteEx(string path, bool recursive)
        {
            try
            {
                Directory.Delete(path, true);
            }
            catch (IOException)
            {
                Directory.Delete(path, true);
            }
            catch (UnauthorizedAccessException)
            {
                Directory.Delete(path, true);
            }
        }
    }

    public static class StringHelper
    {
        /// <summary>
        /// Remove last newline in the string, removes whitespaces also.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveLastNewLine(string str)
        {
            return str.Trim().TrimEnd(Environment.NewLine.ToCharArray()).Trim();
        }
    }

    public enum ParseStringToNumberType
    {
        String, Integer, Decimal
    }

    [Flags]
    public enum CompareStringNumberResult
    {
        None = 0,
        Equal = 1,
        NotEqual = 2,
        Smaller = 4,
        Bigger = 8,
    }

    public static class NumberHelper
    {
        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns>Return false if failed</returns>
        public static bool ParseInt32(string str, out Int32 value)
        {
            if (str == null || string.Equals(str, string.Empty))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Int32.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return Int32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt32(string str, out UInt32 value)
        {
            if (str == null || string.Equals(str, string.Empty))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return UInt32.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return UInt32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns>Return false if failed</returns>
        public static bool ParseInt64(string str, out Int64 value)
        {
            if (str == null || string.Equals(str, string.Empty))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return Int64.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return Int64.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// integer parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseUInt64(string str, out UInt64 value)
        {
            if (str == null || string.Equals(str, string.Empty))
            {
                value = 0;
                return false;
            }

            if (str.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return UInt64.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            else
                return UInt64.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// decimal parser, supports base 10 and 16 at same time
        /// </summary>
        /// <returns></returns>
        public static bool ParseDecimal(string str, out decimal value)
        {
            if (string.Equals(str, string.Empty))
            {
                value = 0;
                return false;
            }
            return decimal.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }



        /// <summary>
        /// Parse string to int or decimal
        /// </summary>
        /// <param name="str"></param>
        /// <param name="integer"></param>
        /// <param name="real"></param>
        /// <returns>Return true if string is number</returns>
        public static ParseStringToNumberType ParseStringToNumber(string str, out int integer, out decimal real)
        {
            integer = 0;
            real = 0;

            if (str == null || string.Equals(str, string.Empty))
                return ParseStringToNumberType.String;

            // base 16 integer - Z
            if (Regex.IsMatch(str, @"^0x\d+$", RegexOptions.Compiled))
            {
                if (Int32.TryParse(str.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out integer))
                    return ParseStringToNumberType.Integer;
                else
                    return ParseStringToNumberType.String;
            }
            // real number - R
            else if (Regex.IsMatch(str, @"^(\d+)\.(\d+)$", RegexOptions.Compiled))
            {
                if (decimal.TryParse(str, NumberStyles.AllowDecimalPoint | NumberStyles.Integer, CultureInfo.InvariantCulture, out real))
                    return ParseStringToNumberType.Decimal;
                else
                    return ParseStringToNumberType.String;
            }
            else
            {
                // integer - Z
                if (Int32.TryParse(str, NumberStyles.Integer, CultureInfo.InvariantCulture, out integer))
                    return ParseStringToNumberType.Integer;
                else
                    return ParseStringToNumberType.String;
            }
        }



        /// <summary>
        /// Compare string, which would be number
        /// </summary>
        /// <param name="str1"></param>
        /// <param name="str2"></param>
        public static CompareStringNumberResult CompareStringNumber(string str1, string str2)
        {
            int num1, num2;
            decimal real1, real2;
            ParseStringToNumberType type1 = ParseStringToNumber(str1, out num1, out real1);
            ParseStringToNumberType type2 = ParseStringToNumber(str2, out num2, out real2);

            if (type1 == ParseStringToNumberType.String || type2 == ParseStringToNumberType.String)
            { // One of arg is string, so just compare
                if (string.Equals(str1, str2, StringComparison.OrdinalIgnoreCase))
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual;
            }
            else if (type1 == ParseStringToNumberType.Integer && type2 == ParseStringToNumberType.Integer)
            { // Args are both int
                int comp = num1 - num2;
                if (comp < 0)
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Bigger;
            }
            else if (type1 == ParseStringToNumberType.Integer && type2 == ParseStringToNumberType.Decimal)
            { // One arg is decimal
                decimal comp = num1 - real2;
                if (comp < 0)
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Bigger;
            }
            else if (type1 == ParseStringToNumberType.Decimal && type2 == ParseStringToNumberType.Integer)
            { // One arg is decimal
                decimal comp = real1 - num2;
                if (comp < 0)
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Bigger;
            }
            else
            { // All args is decimal
                decimal comp = real1 - real2;
                if (comp < 0)
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Smaller;
                else if (comp == 0)
                    return CompareStringNumberResult.Equal;
                else
                    return CompareStringNumberResult.NotEqual | CompareStringNumberResult.Bigger;
            }
        }

        /// <summary>
        /// Parse hex string into byte array. Hex string must be in form of A0B1C2. Return true if success.
        /// </summary>
        /// <param name="hex"></param>
        /// <param name="array"></param>
        /// <returns>Return true if success.</returns>
        public static bool ParseHexStringToByteArray(string hex, out byte[] array)
        {
            if (hex.Length % 2 == 1) // hex's length must be even number
            {
                array = new byte[0];
                return false;
            }

            array = new byte[hex.Length / 2];
            for (int i = 0; i < hex.Length; i += 2)
                array[i/2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return true;
        }
    }

    /// <summary>
    /// Exception used in BakeryEngine::ParseCommand
    /// </summary>
    public class InvalidRegistryKeyException : Exception
    {
        public InvalidRegistryKeyException() { }
        public InvalidRegistryKeyException(string message) : base(message) { }
        public InvalidRegistryKeyException(string message, Exception inner) : base(message, inner) { }
    }

    public static class RegistryHelper
    {
        [DllImport("kernel32.dll")]
        static extern IntPtr GetCurrentProcess();
        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool OpenProcessToken(IntPtr ProcessHandle, UInt32 DesiredAccess, out IntPtr TokenHandle);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out LUID lpLuid);
        [DllImport("advapi32.dll", ExactSpelling = true, SetLastError = true)]
        static extern bool AdjustTokenPrivileges(IntPtr htok, bool disableAllPrivileges, ref TOKEN_PRIVILEGES newState, UInt32 len, IntPtr prev, IntPtr relen);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern Int32 RegLoadKey(UInt32 hKey, string lpSubKey, string lpFile);
        [DllImport("advapi32.dll", SetLastError = true)]
        static extern Int32 RegUnLoadKey(UInt32 hKey, string lpSubKey);
        [DllImport("kernel32.dll", SetLastError = true)]
        [ReliabilityContract(Consistency.WillNotCorruptState, Cer.Success)]
        [SuppressUnmanagedCodeSecurity]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID
        {
            public uint LowPart;
            public int HighPart;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID pLuid;
            public UInt32 Attributes;
        }
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct TOKEN_PRIVILEGES
        {
            public int Count;
            public LUID Luid;
            public UInt32 Attr;
        }

        private const Int32 ANYSIZE_ARRAY = 1;
        private const UInt32 SE_PRIVILEGE_ENABLED = 0x00000002;
        private const UInt32 TOKEN_ADJUST_PRIVILEGES = 0x0020;
        private const UInt32 TOKEN_QUERY = 0x0008;

        public const UInt32 HKCR = 0x80000000; // HKEY_CLASSES_ROOT
        public const UInt32 HKCU = 0x80000001; // HKEY_CURRENT_USER
        public const UInt32 HKLM = 0x80000002; // HKEY_LOCAL_MACHINE
        public const UInt32 HKU = 0x80000003; // HKEY_USERS
        public const UInt32 HKPD = 0x80000004; // HKEY_PERFORMANCE_DATA
        public const UInt32 HKCC = 0x80000005; // HKEY_CURRENT_CONFIG

        public static void HandleWin32Exception(string message)
        {
            int errorCode = Marshal.GetLastWin32Error();
            Win32Exception e = new Win32Exception(errorCode);
            throw new Win32Exception($"{message}, Error [{errorCode}, {e.Message}]");
        }

        public static void GetAdminPrivileges()
        {
            IntPtr hToken;
            TOKEN_PRIVILEGES pRestoreToken = new TOKEN_PRIVILEGES();
            TOKEN_PRIVILEGES pBackupToken = new TOKEN_PRIVILEGES();
            LUID restoreLUID;
            LUID backupLUID;

            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken))
                HandleWin32Exception("OpenProcessToken failed");

            if (!LookupPrivilegeValue(null, "SeRestorePrivilege", out restoreLUID))
                HandleWin32Exception("LookupPrivilegeValue failed");

            if (!LookupPrivilegeValue(null, "SeBackupPrivilege", out backupLUID))
                HandleWin32Exception("LookupPrivilegeValue failed");

            pRestoreToken.Count = 1;
            pRestoreToken.Luid = restoreLUID;
            pRestoreToken.Attr = SE_PRIVILEGE_ENABLED;

            pBackupToken.Count = 1;
            pBackupToken.Luid = backupLUID;
            pBackupToken.Attr = SE_PRIVILEGE_ENABLED;

            if (!AdjustTokenPrivileges(hToken, false, ref pRestoreToken, 0, IntPtr.Zero, IntPtr.Zero))
                HandleWin32Exception("AdjustTokenPrivileges failed");
            if (Marshal.GetLastWin32Error() == ResultWin32.ERROR_NOT_ALL_ASSIGNED)
                throw new Win32Exception($"AdjustTokenPrivileges failed, Try running this program with Administrator privilege.");
            CloseHandle(hToken);

            if (!AdjustTokenPrivileges(hToken, false, ref pBackupToken, 0, IntPtr.Zero, IntPtr.Zero))
                HandleWin32Exception("AdjustTokenPrivileges failed");
            if (Marshal.GetLastWin32Error() == ResultWin32.ERROR_NOT_ALL_ASSIGNED)
                throw new Win32Exception($"AdjustTokenPrivileges failed, Try running this program with Administrator privilege.");
            CloseHandle(hToken);
        }

        public static RegistryKey ParseRootKeyToRegKey(string rootKey)
        {
            return InternalParseRootKeyToRegKey(rootKey, false);
        }

        public static RegistryKey ParseRootKeyToRegKey(string rootKey, bool exception)
        {
            return InternalParseRootKeyToRegKey(rootKey, exception);
        }

        public static RegistryKey InternalParseRootKeyToRegKey(string rootKey, bool exception)
        {
            RegistryKey regRoot;
            if (string.Equals(rootKey, "HKCR", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.ClassesRoot; // HKEY_CLASSES_ROOT
            else if (string.Equals(rootKey, "HKCU", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.CurrentUser; // HKEY_CURRENT_USER
            else if (string.Equals(rootKey, "HKLM", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.LocalMachine; // HKEY_LOCAL_MACHINE
            else if (string.Equals(rootKey, "HKU", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.Users; // HKEY_USERS
            else if (string.Equals(rootKey, "HKPD", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.PerformanceData; // HKEY_PERFORMANCE_DATA
            else if (string.Equals(rootKey, "HKCC", StringComparison.OrdinalIgnoreCase))
                regRoot = Registry.CurrentConfig; // HKEY_CURRENT_CONFIG
            else
            {
                if (exception)
                    throw new InvalidRegistryKeyException();
                else
                    regRoot = null;
            }
            return regRoot;
        }

        public static UInt32 ParseRootKeyToUInt32(string rootKey)
        {
            return InternalParseRootKeyToUInt32(rootKey, false);
        }

        public static UInt32 ParseRootKeyToUInt32(string rootKey, bool exception)
        {
            return InternalParseRootKeyToUInt32(rootKey, exception);
        }

        public static UInt32 InternalParseRootKeyToUInt32(string rootKey, bool exception)
        {
            UInt32 hKey;
            if (string.Equals(rootKey, "HKCR", StringComparison.OrdinalIgnoreCase))
                hKey = HKCR;
            else if (string.Equals(rootKey, "HKCU", StringComparison.OrdinalIgnoreCase))
                hKey = HKCU;
            else if (string.Equals(rootKey, "HKLM", StringComparison.OrdinalIgnoreCase))
                hKey = HKLM;
            else if (string.Equals(rootKey, "HKU", StringComparison.OrdinalIgnoreCase))
                hKey = HKU;
            else if (string.Equals(rootKey, "HKPD", StringComparison.OrdinalIgnoreCase))
                hKey = HKPD;
            else if (string.Equals(rootKey, "HKCC", StringComparison.OrdinalIgnoreCase))
                hKey = HKCC;
            else
            {
                if (exception)
                    throw new InvalidRegistryKeyException();
                else
                    hKey = 0;
            }
            return hKey;
        }
    }

    public static class CompressHelper
    {
        /// <summary>
        /// Expand cab file using P/invoked FDICreate, FDICopy, FDIDestroy
        /// </summary>
        /// TODO: Use 
        /// </remarks>
        /// <param name="srcPath"></param>
        /// <param name="destPath"></param>
        /// <returns>Return true if success</returns>
        public static bool ExtractCab(string srcCabFile, string destDir)
        {
            List<string> nop;
            CabExtract.CabExtract cab = new CabExtract.CabExtract(srcCabFile);
            return cab.ExtractAll(destDir, out nop);
        }

        /// <summary>
        /// Expand cab file using P/invoked FDICreate, FDICopy, FDIDestroy
        /// </summary>
        /// TODO: Use 
        /// </remarks>
        /// <param name="srcPath"></param>
        /// <param name="destPath"></param>
        /// <returns>Return true if success</returns>
        public static bool ExtractCab(string srcCabFile, string destDir, out List<string> extractedList)
        {
            CabExtract.CabExtract cab = new CabExtract.CabExtract(srcCabFile);
            return cab.ExtractAll(destDir, out extractedList);
        }

        /// <summary>
        /// Expand cab file using P/invoked FDICreate, FDICopy, FDIDestroy
        /// </summary>
        /// TODO: Use 
        /// </remarks>
        /// <param name="srcPath"></param>
        /// <param name="destPath"></param>
        /// <returns>Return true if success</returns>
        public static bool ExtractCab(string srcCabFile, string destDir, string target)
        {
            CabExtract.CabExtract cab = new CabExtract.CabExtract(srcCabFile);
            return cab.ExtractSingleFile(target, destDir);
        }        
    }
}
