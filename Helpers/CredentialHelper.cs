using System.Runtime.InteropServices;
using System.Text;

namespace PersonelTakip.Monitoring.Helpers;

public static class CredentialHelper
{
    private const string TargetName = "PersonelTakipWPF";

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CREDENTIAL
    {
        public uint Flags;
        public uint Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite([In] ref CREDENTIAL credential, [In] uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree([In] IntPtr credential);

    private const uint CRED_TYPE_GENERIC = 1;
    private const uint CRED_PERSIST_LOCAL_MACHINE = 2;

    public static void SaveCredential(string email, string password)
    {
        var passwordBytes = Encoding.Unicode.GetBytes(password);
        var passwordPtr = Marshal.AllocHGlobal(passwordBytes.Length);
        Marshal.Copy(passwordBytes, 0, passwordPtr, passwordBytes.Length);

        var credential = new CREDENTIAL
        {
            Type = CRED_TYPE_GENERIC,
            TargetName = Marshal.StringToCoTaskMemUni(TargetName),
            CredentialBlobSize = (uint)passwordBytes.Length,
            CredentialBlob = passwordPtr,
            Persist = CRED_PERSIST_LOCAL_MACHINE,
            UserName = Marshal.StringToCoTaskMemUni(email)
        };

        CredWrite(ref credential, 0);

        Marshal.FreeHGlobal(passwordPtr);
        Marshal.FreeCoTaskMem(credential.TargetName);
        Marshal.FreeCoTaskMem(credential.UserName);
    }

    public static (string? email, string? password) LoadCredential()
    {
        if (CredRead(TargetName, CRED_TYPE_GENERIC, 0, out IntPtr credentialPtr))
        {
            try
            {
                var credential = Marshal.PtrToStructure<CREDENTIAL>(credentialPtr);
                var email = Marshal.PtrToStringUni(credential.UserName);
                var passwordBytes = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, passwordBytes, 0, (int)credential.CredentialBlobSize);
                var password = Encoding.Unicode.GetString(passwordBytes);
                return (email, password);
            }
            finally
            {
                CredFree(credentialPtr);
            }
        }
        return (null, null);
    }

    public static void DeleteCredential()
    {
        CredDelete(TargetName, CRED_TYPE_GENERIC, 0);
    }
}