Imports NUnit.Framework
Imports PGPKeyDesk.Services

<TestFixture>
Public Class KeyExpiryTests
    Private ReadOnly Now_ As New DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)

    <Test>
    Public Sub NoExpiry_IsNeverExpires()
        Assert.That(KeyExpiry.Evaluate(Nothing, Now_), [Is].EqualTo(KeyExpiryStatus.NeverExpires))
    End Sub

    <Test>
    Public Sub Past_IsExpired()
        Assert.That(KeyExpiry.Evaluate(Now_.AddSeconds(-1), Now_), [Is].EqualTo(KeyExpiryStatus.Expired))
    End Sub

    <Test>
    Public Sub ExactlyNow_IsExpired()
        Assert.That(KeyExpiry.Evaluate(Now_, Now_), [Is].EqualTo(KeyExpiryStatus.Expired))
    End Sub

    <Test>
    Public Sub WithinWarnWindow_IsExpiringSoon()
        Assert.That(KeyExpiry.Evaluate(Now_.AddDays(29), Now_), [Is].EqualTo(KeyExpiryStatus.ExpiringSoon))
        Assert.That(KeyExpiry.Evaluate(Now_.AddDays(30), Now_), [Is].EqualTo(KeyExpiryStatus.ExpiringSoon))
    End Sub

    <Test>
    Public Sub BeyondWarnWindow_IsValid()
        Assert.That(KeyExpiry.Evaluate(Now_.AddDays(31), Now_), [Is].EqualTo(KeyExpiryStatus.Valid))
    End Sub

    <Test>
    Public Sub CustomWarnDays_Respected()
        Assert.That(KeyExpiry.Evaluate(Now_.AddDays(10), Now_, 7), [Is].EqualTo(KeyExpiryStatus.Valid))
        Assert.That(KeyExpiry.Evaluate(Now_.AddDays(10), Now_, 14), [Is].EqualTo(KeyExpiryStatus.ExpiringSoon))
    End Sub

    <Test>
    Public Sub Describe_IsReadable()
        Assert.That(KeyExpiry.Describe(Nothing, Now_), [Is].EqualTo("never expires"))
        Assert.That(KeyExpiry.Describe(Now_.AddDays(-2), Now_), Does.StartWith("EXPIRED on 2026-05-30"))
        Assert.That(KeyExpiry.Describe(Now_.AddDays(5), Now_), Does.Contain("expires in 5 day(s)"))
        Assert.That(KeyExpiry.Describe(Now_.AddDays(90), Now_), Does.StartWith("valid until"))
    End Sub

    <Test>
    Public Sub Earliest_HandlesNothing()
        Dim a As DateTime? = Now_
        Dim b As DateTime? = Now_.AddDays(1)
        Assert.That(KeyExpiry.Earliest(a, b), [Is].EqualTo(a))
        Assert.That(KeyExpiry.Earliest(b, a), [Is].EqualTo(a))
        Assert.That(KeyExpiry.Earliest(Nothing, b), [Is].EqualTo(b))
        Assert.That(KeyExpiry.Earliest(a, Nothing), [Is].EqualTo(a))
        Assert.That(KeyExpiry.Earliest(Nothing, Nothing), [Is].Null)
    End Sub
End Class
