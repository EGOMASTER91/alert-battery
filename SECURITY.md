# Security / Securitate

## English

Supported version: 1.0.1. Report security issues through this repository's private vulnerability reporting feature. Do not include personal settings, ntfy topics, serial numbers, Bluetooth addresses or hardware diagnostics in public issues.

The app reads battery/identification information using explicit protocols. It does not update firmware, alter DPI/polling profiles or install a driver. Product images are local; no analytics is implemented. Network traffic is limited to user-enabled ntfy alerts over HTTPS to the configured server. The notification includes the device name and battery percentage; automatic HTTP redirects are disabled. Phone notifications are disabled by default.

A public ntfy topic is not authenticated. Anyone who knows its name may access it or publish to it. Local settings, alert state and diagnostics are not application-encrypted; they remain under the Windows user's profile permissions. Diagnostic exports can contain hardware identifiers and should remain private.

Release packages exclude personal settings, logs, inventories, test output, symbols and build paths. Included third-party licenses and required HeadsetControl source remain intact. Download the full ZIP and verify its SHA-256 digest against `SHA256SUMS.txt`.

Automated scans and tests check defined cases and currently reported dependencies; they do not prove the absence of every vulnerability. See `docs/SECURITY-AUDIT.md` for the performed checks and scope.

## Română

Versiune întreținută: 1.0.1. Raportează problemele prin funcția de raportare privată a vulnerabilităților din acest repository. Nu include în issue-uri publice setări personale, canale ntfy, serii, adrese Bluetooth sau diagnostice hardware.

Aplicația citește bateria și identificarea prin protocoale explicite. Nu modifică firmware, DPI sau profiluri de polling și nu instalează un driver. Fotografiile sunt locale; nu există analiză de utilizare. Traficul de rețea se limitează la alertele ntfy activate de utilizator, prin HTTPS către serverul configurat. Notificarea conține numele și procentul bateriei; redirecționările HTTP automate sunt dezactivate. Alertele pe telefon sunt dezactivate implicit.

Un canal ntfy public nu este autentificat. Cine îi cunoaște numele poate să îl acceseze sau să publice. Setările, starea și diagnosticele locale nu sunt criptate suplimentar de aplicație; rămân sub permisiunile profilului Windows. Exporturile de diagnostic pot conține identificatori hardware și trebuie păstrate private.

Pachetele nu includ setări personale, loguri, inventare, rezultate de teste, simboluri de depanare sau căi locale de compilare. Licențele și sursele obligatorii HeadsetControl sunt păstrate. Verifică arhiva folosind `SHA256SUMS.txt`.

Scanările și testele verifică un domeniu definit și vulnerabilitățile raportate pentru dependențe; nu dovedesc absența oricărei vulnerabilități. Verificările efectuate sunt descrise în `docs/SECURITY-AUDIT.md`.
