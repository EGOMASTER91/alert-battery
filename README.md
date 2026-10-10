# Battery Alert

[English](#english) · [Română](#română) · [Releases](https://github.com/EGOMASTER91/alert-battery/releases)

## English

Battery Alert is a local Windows 11 x64 application that monitors supported **wireless headsets, mice and keyboards** and alerts you when their battery is low. No account or subscription is required. The application interface is currently in Romanian; this documentation and release notes are available in both languages.

### Features

- Windows notifications and optional phone notifications through HTTPS / ntfy.
- Default alert threshold: **10%**; battery checks every **60 seconds**.
- Per-device thresholds, monitoring, notification destinations and sound controls.
- One alert per discharge cycle, retained across restarts; rearmed after recovery by five percentage points. Charging suspends alerts when reported by the device.
- Tray operation, pause/resume, optional startup at Windows sign-in, product images and custom photographs.
- Wireless headsets, mice and keyboards only in the main dashboard. Known wireless products remain visible while charging over a USB cable. Unknown USB products are listed on the Compatibility page.
- Missing, stale or approximate readings are never converted into invented percentages.

### Download and run

Download a ZIP from [Releases](https://github.com/EGOMASTER91/alert-battery/releases), extract it completely and open the **BatteryAlert.exe at the top of the extracted folder**. Keep its `dist` folder beside it.

| Package | Requirements |
| --- | --- |
| `BatteryAlert-v1.0.1-win-x64-local.zip` | Smaller package; requires **.NET 10 Desktop Runtime x64 (10.0.9 or newer)** already installed. |
| `BatteryAlert-v1.0.1-win-x64-portable.zip` | Includes the .NET runtime; no SDK or separate .NET installation required. |

Both packages include the required HeadsetControl helper, licenses and its corresponding source archive. Source code, automated tests and build directories are excluded from the application packages, except legally required third-party source material. Verify downloads using the release's `SHA256SUMS.txt`.

Closing the window keeps monitoring active in the tray. Right-click the tray icon to open, pause or exit. Save startup settings again if you move the application folder.

### Phone setup and privacy

1. Install the ntfy application on your phone.
2. Subscribe to the HTTPS server and randomly generated topic shown in **Alerte și telefon**.
3. Enable phone alerts, save settings and use the phone test button.

Phone alerts are optional. When enabled, the configured ntfy server receives the alert's device name and battery percentage. There is no analytics or automatic upload of the device inventory. Automatic HTTP redirects are disabled. On public ntfy servers, the topic name is a shared secret, not account authentication; keep it private. This version does not implement ntfy authentication. Offline Windows alerts continue to work; phone retries are limited and alerts expire after 15 minutes.

Settings, alert state, optional local photographs and limited diagnostic logs stay under `%LOCALAPPDATA%/BatteryAlert`. Settings are protected by the Windows user profile permissions and are not application-encrypted. Do not publish state files, diagnostics or USB/Bluetooth inventories. No personal state is included in this repository or its application packages. See [Security policy](SECURITY.md).

### Compatibility and validation

Providers include HeadsetControl, explicitly identified Pulsar Fusion and CrazyLight/Bibimbap devices, Windows battery properties, standard BLE Battery Service and Logitech HID++ 2.0. Support depends on each model's accessible protocol; compatibility with every wireless peripheral is not promised.

Logitech PRO X 2 LIGHTSPEED and Pulsar Xlite V3 have previous physical validation. PRO X Wireless, CrazyLight, Logitech HID++ receivers and a compatible wireless keyboard require further physical validation. See the [compatibility matrix](docs/COMPATIBILITY.md), [validation record](docs/VALIDATION.md) and [security checks](docs/SECURITY-AUDIT.md).

### Development

Requires Windows and .NET SDK 10. The repository contains tests for maintainers; the downloadable application does not.

```powershell
dotnet build BatteryAlert.sln -c Release
dotnet test BatteryAlert.sln -c Release
./scripts/publish.ps1 -Local
./scripts/publish.ps1
```

The first publish command produces the smaller package; the second produces the portable package. `--render-preview <path.png>` uses demonstration data. `--smoke-test <path.json>` uses isolated test settings and sends a silent Windows notification. `--diagnose <path.json>` writes a local hardware inventory: keep its output private.

## Română

Battery Alert este o aplicație locală pentru Windows 11 x64 care monitorizează **căști, mouse-uri și tastaturi wireless compatibile** și trimite alerte când bateria este scăzută. Nu necesită cont sau abonament. Interfața aplicației este în română; documentația și notele de release sunt în română și engleză.

### Funcții

- Notificări Windows și, opțional, alerte pe telefon prin HTTPS / ntfy.
- Prag implicit **10%**, cu verificări la fiecare **60 de secunde**.
- Prag, monitorizare, destinații și sunet configurabile pentru fiecare dispozitiv.
- O alertă pe ciclu de descărcare, păstrată după restart; reactivare după recuperarea a cinci puncte procentuale. Încărcarea suspendă alerta când dispozitivul o raportează.
- Monitorizare lângă ceas, pauză/reluare, pornire opțională la autentificare, fotografii de produs și imagini personalizate.
- Lista principală afișează numai periferice confirmate wireless. Modelele wireless cunoscute rămân vizibile când sunt conectate prin cablu pentru încărcare. Dispozitivele USB neclasificate apar în pagina Compatibilitate.
- Datele lipsă, vechi sau aproximative nu sunt transformate în procente inventate.

### Descărcare și pornire

Descarcă o arhivă ZIP din [Releases](https://github.com/EGOMASTER91/alert-battery/releases), extrage-o integral și deschide **BatteryAlert.exe din rădăcina folderului extras**. Păstrează folderul `dist` lângă executabil.

| Pachet | Cerințe |
| --- | --- |
| `BatteryAlert-v1.0.1-win-x64-local.zip` | Pachet mai mic; necesită **.NET 10 Desktop Runtime x64 (10.0.9 sau mai nou)** instalat. |
| `BatteryAlert-v1.0.1-win-x64-portable.zip` | Include runtime-ul .NET; nu necesită SDK sau instalarea separată a .NET. |

Ambele includ HeadsetControl, licențele și arhiva obligatorie de surse a componentei. Sursele aplicației, testele și fișierele de compilare sunt excluse din pachetele de rulare; sursele terțe cerute de licență rămân incluse. Verifică descărcarea folosind `SHA256SUMS.txt` din release.

Închiderea ferestrei păstrează monitorizarea activă lângă ceas. Meniul iconiței oferă deschidere, pauză și ieșire. Dacă muți folderul aplicației, salvează din nou opțiunea de pornire automată.

### Telefon și confidențialitate

1. Instalează aplicația ntfy pe telefon.
2. Abonează-te la serverul HTTPS și canalul generat aleatoriu afișate în **Alerte și telefon**.
3. Activează alertele pe telefon, salvează și folosește butonul de test.

Alertele pe telefon sunt opționale. Când le activezi, serverul ntfy configurat primește numele dispozitivului și procentul bateriei din alertă. Nu există analiză de utilizare sau încărcarea automată a inventarului. Redirecționările HTTP automate sunt dezactivate. Pe serverele ntfy publice, numele canalului este un secret comun, nu autentificare prin cont; păstrează-l privat. Versiunea nu implementează autentificare ntfy. Alertele Windows funcționează fără internet; reîncercările pe telefon sunt limitate, iar mesajele expiră după 15 minute.

Setările, starea alertelor, fotografiile opționale și logurile limitate rămân în `%LOCALAPPDATA%/BatteryAlert`. Setările sunt protejate prin permisiunile profilului Windows, fără criptare suplimentară în aplicație. Nu publica fișierele de stare, diagnosticele sau inventarele USB/Bluetooth. Repository-ul și pachetele nu includ configurații personale. Vezi [politica de securitate](SECURITY.md).

### Compatibilitate și verificare

Adaptoarele includ HeadsetControl, dispozitive Pulsar Fusion și CrazyLight/Bibimbap identificate explicit, proprietățile Windows, BLE Battery Service și Logitech HID++ 2.0. Suportul depinde de protocolul fiecărui model; compatibilitatea cu orice periferic wireless nu este promisă.

Logitech PRO X 2 LIGHTSPEED și Pulsar Xlite V3 au validări fizice anterioare. PRO X Wireless, CrazyLight, receiverele Logitech HID++ și o tastatură wireless compatibilă necesită verificări fizice suplimentare. Vezi [matricea](docs/COMPATIBILITY.md), [validarea](docs/VALIDATION.md) și [verificările de securitate](docs/SECURITY-AUDIT.md).

### Dezvoltare

Necesită Windows și .NET SDK 10. Testele sunt păstrate în repository pentru întreținere; nu sunt incluse în aplicația descărcabilă.

```powershell
dotnet build BatteryAlert.sln -c Release
dotnet test BatteryAlert.sln -c Release
./scripts/publish.ps1 -Local
./scripts/publish.ps1
```

Prima comandă de publicare produce pachetul mic; a doua produce pachetul portabil. `--render-preview <path.png>` folosește date demonstrative. `--smoke-test <path.json>` utilizează setări de test separate și trimite o notificare Windows silențioasă. `--diagnose <path.json>` scrie un inventar local: păstrează rezultatul privat.

Third-party notices / Licențe terțe: [THIRD-PARTY-NOTICES.txt](THIRD-PARTY-NOTICES.txt), `vendor/`, [image sources / sursele imaginilor](docs/DESIGN-ASSETS.md).


