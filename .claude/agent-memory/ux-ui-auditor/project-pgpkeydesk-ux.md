---
name: project-pgpkeydesk-ux
description: UX/UI state of the PGPKeyDesk WPF application — design decisions, fixed issues, and known patterns
metadata:
  type: project
---

PGPKeyDesk is a WPF .NET application (VB.NET) for PGP encryption/decryption with a dark Catppuccin-inspired theme (#1E1E2E background, #89B4FA blue accent, #A6E3A1 green, #F38BA8 red).

**Architecture:**
- MainWindow: two-panel layout (240px sidebar + right content area with TabControl)
- Two tabs: Decrypt and Encrypt
- Three dialog windows: AddProfileWindow (NoResize), GenerateKeyPairWindow (CanResizeWithGrip)

**Fixed Issues (2026-06-27, batch 1):**
- Scroll bug in all 4 text areas (StackPanel→Grid with Auto/* rows). TxtDecrypted initial Foreground fixed to #45475A. BtnEncrypt VerticalAlignment="Center" added.
- AddProfileWindow: ResizeMode NoResize→CanResizeWithGrip, Height 740→680, MinHeight="600", key rows Height→"*" MinHeight="120".
- Passphrase PasswordBox flashes red border 1.5s on wrong passphrase (FlashPassphraseError). Ctrl+Enter triggers active tab action.
- Status bar auto-fades success/info messages to "Ready" after 3.5s.
- Decrypt/Encrypt Clear buttons aligned to VerticalAlignment="Center". ClearEncrypt resets TxtEncryptedOutput.Foreground to #89DCEB.
- Terminology unified: label "Passphrase" everywhere.

**Fixed Issues (2026-06-27, batch 2):**
- Sidebar profiles: StackPanel → 3-row Grid (Auto/*/Auto). ListBox now scrolls with many profiles. Added ScrollViewer.HorizontalScrollBarVisibility="Disabled".
- Right panel: removed empty Height="Auto" Row 1. TabControl now Grid.Row=1, StatusBar Grid.Row=2.
- InputBox + PasswordInput styles: focus trigger → BorderBrush="#89B4FA" when IsKeyboardFocused="True".
- Sidebar Border: added right separator border (BorderThickness="0,0,1,0" BorderBrush="#313244").
- Encrypt tab: added HintPlaintext TextBlock overlay (was missing). TxtPlaintext_Changed now shows/hides it. ClearEncrypt_Click resets it.
- Decrypt/Encrypt action rows: 20px spacer columns replaced with Margin="0,0,8,0" on Clear button.
- GenerateKeyPairWindow: custom RadioButton style (Catppuccin: #89B4FA indicator dot, #45475A ring, cursor Hand).
- GenerateKeyPairWindow Input/PasswordInput: focus triggers added.
- AddProfileWindow Input: SelectionBrush="#89B4FA" and focus trigger added. Content wrapped in ScrollViewer.

**Why:** StackPanel wrappers inside Height="*" Grid rows never stretch. All four content areas had this pattern.
**How to apply:** Always use Grid with row definitions (not StackPanel) when a child needs to fill available * space.

**Established Design Tokens:**
- Background primary: #1E1E2E
- Background secondary: #181825
- Surface: #313244
- Surface hover: #45475A
- Text primary: #CDD6F4
- Text secondary: #A6ADC8
- Text muted: #6C7086
- Text placeholder: #45475A
- Accent blue: #89B4FA / hover #B4D0FF
- Green: #A6E3A1
- Red: #F38BA8
- Yellow: #F9E2AF
- Cyan: #89DCEB (encrypt output foreground)

**Button styles defined in MainWindow.xaml:** PrimaryBtn (blue), EncryptBtn (green), DangerBtn (red outline), AddBtn (surface), GenerateBtn (surface+yellow text)

**Code-behind note:** TxtDecrypted foreground is changed dynamically in code — green on success, red on error, #45475A on reset. TxtEncryptedOutput foreground is never changed in code (always #89DCEB from XAML).
