Imports System.Security.Cryptography

Namespace Services
    ''' <summary>Encrypts/decrypts the profile store at rest (abstracted so storage logic is testable).</summary>
    Public Interface ISecretProtector
        Function Protect(plain As Byte()) As Byte()
        ''' <summary>Throws CryptographicException if the data cannot be decrypted.</summary>
        Function Unprotect(protectedData As Byte()) As Byte()
    End Interface

    ''' <summary>Windows DPAPI, bound to the current Windows user.</summary>
    Public Class DpapiProtector
        Implements ISecretProtector

        Public Function Protect(plain As Byte()) As Byte() Implements ISecretProtector.Protect
            Return System.Security.Cryptography.ProtectedData.Protect(plain, Nothing, DataProtectionScope.CurrentUser)
        End Function

        Public Function Unprotect(protectedData As Byte()) As Byte() Implements ISecretProtector.Unprotect
            Return System.Security.Cryptography.ProtectedData.Unprotect(protectedData, Nothing, DataProtectionScope.CurrentUser)
        End Function
    End Class
End Namespace
