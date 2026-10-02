Imports System.Globalization

Namespace Services
    Public Enum KeyExpiryStatus
        NeverExpires
        Valid
        ExpiringSoon
        Expired
    End Enum

    ''' <summary>Pure expiry logic (time is injected so it is unit-testable).</summary>
    Public Class KeyExpiry
        Public Const DefaultWarnDays As Integer = 30

        Public Shared Function Evaluate(expiresAtUtc As DateTime?, nowUtc As DateTime,
                                        Optional warnDays As Integer = DefaultWarnDays) As KeyExpiryStatus
            If Not expiresAtUtc.HasValue Then Return KeyExpiryStatus.NeverExpires
            If expiresAtUtc.Value <= nowUtc Then Return KeyExpiryStatus.Expired
            If expiresAtUtc.Value <= nowUtc.AddDays(warnDays) Then Return KeyExpiryStatus.ExpiringSoon
            Return KeyExpiryStatus.Valid
        End Function

        Public Shared Function Describe(expiresAtUtc As DateTime?, nowUtc As DateTime,
                                        Optional warnDays As Integer = DefaultWarnDays) As String
            Dim ci = CultureInfo.InvariantCulture
            Select Case Evaluate(expiresAtUtc, nowUtc, warnDays)
                Case KeyExpiryStatus.NeverExpires
                    Return "never expires"
                Case KeyExpiryStatus.Expired
                    Return "EXPIRED on " & expiresAtUtc.Value.ToString("yyyy-MM-dd", ci)
                Case KeyExpiryStatus.ExpiringSoon
                    Dim days = Math.Max(0, CInt(Math.Ceiling((expiresAtUtc.Value - nowUtc).TotalDays)))
                    Return "expires in " & days.ToString(ci) & " day(s) (" & expiresAtUtc.Value.ToString("yyyy-MM-dd", ci) & ")"
                Case Else
                    Return "valid until " & expiresAtUtc.Value.ToString("yyyy-MM-dd", ci)
            End Select
        End Function

        ''' <summary>Earlier of two optional expiry times (Nothing = never).</summary>
        Public Shared Function Earliest(a As DateTime?, b As DateTime?) As DateTime?
            If Not a.HasValue Then Return b
            If Not b.HasValue Then Return a
            Return If(a.Value <= b.Value, a, b)
        End Function
    End Class
End Namespace
