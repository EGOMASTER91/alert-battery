# Security checks / Verificări de securitate — v1.0.1

Date / Data: 2026-10-10.

## English

Checks performed before public release:

- **75 automated tests passed**, including untrusted ntfy response handling, invalid configuration recovery, protocol parsing, device classification, alert state and timeouts.
- NuGet dependency audit, including transitive packages: **no reported vulnerable packages** at scan time. The portable package contains .NET **10.0.9**; the small package requires Desktop Runtime 10.0.9 or newer.
- Gitleaks **8.30.1**: source snapshot, release package files and the included HeadsetControl corresponding-source archive scanned. **No secrets found after review of one false positive**: an upstream public GPG fingerprint, restricted to its exact archive path and value in `.gitleaks.toml`. No broad third-party exclusion is used.
- Privacy checks on source and application-package files, including UTF-8/UTF-16 binary strings: **no matched personal identity, local user paths or personal ntfy topic**. No state, inventory, diagnostic logs, PDB files or test results are shipped. The public repository is created from a fresh snapshot, without the private archive's history.
- Windows Defender custom scan of both distribution variants: **no detected threats**.
- Both packages started through their top-level launcher; interface/artwork loaded and Windows accepted a **silent test notification** using isolated test settings.
- Manual review: HTTPS-only ntfy configuration; no automatic redirects; no buffered server response body; bounded phone retries; private endpoint details omitted from delivery-error logs; controlled local image paths; exact helper/source checksums verified; no telemetry implementation found.
- GitHub CI checks build, tests, dependency vulnerabilities and secrets. Actions use pinned commit hashes and read-only repository permissions.

Scope: these are defined automated checks and a source review, not a penetration test or a guarantee of zero vulnerabilities. The public ntfy topic is not authenticated, and local settings are not application-encrypted. Hardware models without physical validation and phone delivery after personal setup remain listed in the compatibility matrix. Personal diagnostics and raw audit output are kept out of public documentation.

## Română

Verificări înainte de release-ul public:

- **75 de teste automate trecute**, inclusiv răspunsuri ntfy nesigure, recuperarea configurației, parsarea protocoalelor, clasificarea dispozitivelor, starea alertelor și timeout.
- Audit NuGet cu dependențe tranzitive: **niciun pachet vulnerabil raportat** la momentul scanării. Pachetul portabil include .NET **10.0.9**; varianta mică necesită Desktop Runtime 10.0.9 sau mai nou.
- Gitleaks **8.30.1**: surse, fișierele pachetelor și arhiva de surse HeadsetControl verificate. **Niciun secret găsit după verificarea unui fals pozitiv**: amprenta publică GPG a proiectului terț, permisă numai la calea și valoarea exacte din `.gitleaks.toml`. Nu este exclus generic codul terț.
- Verificare a surselor și binarelor în UTF-8/UTF-16: **nicio potrivire cu identitatea personală, căile profilului local sau canalul ntfy personal**. Nu se distribuie stare, inventare, loguri, PDB sau rezultate de teste. Repository-ul public pornește dintr-un snapshot nou, fără istoricul arhivei private.
- Scanare Windows Defender pentru ambele variante: **nicio amenințare detectată**.
- Ambele pachete au pornit prin executabilul din rădăcină, au încărcat interfața/fotografiile, iar Windows a acceptat o **notificare silențioasă** cu setări de test separate.
- Verificare manuală: ntfy numai prin HTTPS, fără redirecționări automate sau încărcarea corpului răspunsului; reîncercări limitate; endpoint-urile nu apar în logurile erorilor de livrare; căi controlate pentru imagini; checksum-uri verificate pentru helper/surse; fără analiză de utilizare identificată în cod.
- CI GitHub verifică compilarea, testele, dependențele și secretele. Acțiunile sunt fixate la commit și au permisiuni numai de citire.

Aceste verificări și analiza codului au un domeniu definit; nu constituie un test de penetrare sau o garanție că nu există nicio vulnerabilitate. Canalul ntfy public nu este autentificat, iar setările locale nu sunt criptate suplimentar de aplicație. Compatibilitatea hardware neverificată și primirea pe telefon după configurare rămân marcate în matrice. Diagnosticele personale și rapoartele brute nu sunt publicate.
