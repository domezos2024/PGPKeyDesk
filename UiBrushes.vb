Imports System.Windows.Media

''' <summary>Shared, frozen brushes by hex colour: parsed once instead of on every status update.</summary>
Friend Module UiBrushes
    Private ReadOnly Cache As New Dictionary(Of String, SolidColorBrush)(StringComparer.OrdinalIgnoreCase)

    Public Function FromHex(hex As String) As SolidColorBrush
        Dim b As SolidColorBrush = Nothing
        If Not Cache.TryGetValue(hex, b) Then
            b = New SolidColorBrush(CType(ColorConverter.ConvertFromString(hex), Color))
            b.Freeze()
            Cache(hex) = b
        End If
        Return b
    End Function
End Module
