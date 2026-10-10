# Compatibility / Compatibilitate

## English

Wireless identification and battery support are separate. Only confirmed wireless headsets, mice and keyboards appear in the main dashboard. Unknown devices are explained on the Compatibility page. No universal hardware compatibility is claimed.

| Model / family | Battery provider | Validation |
| --- | --- | --- |
| Logitech PRO X 2 LIGHTSPEED | HeadsetControl 4.1.0 | Previously physically validated. |
| Pulsar Xlite V3 | Explicit Fusion POWER receiver protocol | Previously physically validated with launcher closed; no claim of a fresh hardware reading for this release. |
| Logitech PRO X Wireless | HeadsetControl | Implemented; not physically validated. |
| Pulsar CrazyLight / Bibimbap | Explicit cMouse POWER profiles | Implemented; not physically validated. |
| Logitech HID++ 2.0 mice and keyboards | Receiver slots or radio identity; battery features 1000 / 1004 | Implemented; not physically validated. Approximate levels, voltage-only reports and HID++ 1.0 do not produce percentages. |
| Bluetooth headsets, mice and keyboards | Windows driver battery property | A driver reading was previously observed; accuracy and other models remain to be verified. |
| Bluetooth LE HID | Standard Battery Service 180F / Battery Level 2A19 | Implemented; physical keyboard validation pending. |
| Other catalogued wireless headsets | HeadsetControl, model-dependent | Implemented; individual models need validation. |
| Pulsar keyboard receiver F809 | Identified wireless | Battery protocol unsupported. |
| Unknown USB devices | No assumed proprietary protocol | Unclassified/unsupported; shown on Compatibility page. |
| Exclusively wired peripherals | Excluded | Not monitored. |

Known wireless devices can remain visible over USB while charging. Charging suspends alerts only when explicitly reported. Device interfaces are merged only with physical identity evidence; VID/PID or model name alone is insufficient. Disconnected or stale readings are not zero. Multiple valid BLE battery levels use the lowest percentage.

Pending hardware scenarios: PRO X Wireless, CrazyLight, a wireless keyboard, a shared Logitech receiver with multiple peripherals, charging, reconnect, suspend/resume and full-screen games. Phone delivery to a locked phone remains to be confirmed after personal ntfy setup.

## Română

Identificarea wireless și suportul bateriei sunt separate. Dashboard-ul conține numai căști, mouse-uri și tastaturi confirmate wireless. Pagina Compatibilitate explică dispozitivele necunoscute. Nu se declară compatibilitate universală.

| Model / familie | Citirea bateriei | Validare |
| --- | --- | --- |
| Logitech PRO X 2 LIGHTSPEED | HeadsetControl 4.1.0 | Validat fizic anterior. |
| Pulsar Xlite V3 | Protocol Fusion POWER pentru receivere explicite | Validat fizic anterior cu launcherul închis; release-ul nu declară o citire hardware nouă. |
| Logitech PRO X Wireless | HeadsetControl | Implementat, neverificat fizic. |
| Pulsar CrazyLight / Bibimbap | Profiluri cMouse POWER explicite | Implementat, neverificat fizic. |
| Mouse-uri și tastaturi Logitech HID++ 2.0 | Sloturi receiver sau identitate radio; funcțiile 1000 / 1004 | Implementat, neverificat fizic. Nivelurile aproximative, doar voltajul și HID++ 1.0 nu produc procente. |
| Bluetooth: căști, mouse-uri, tastaturi | Proprietatea driverului Windows | O citire a driverului a fost observată anterior; acuratețea și alte modele necesită verificare. |
| Bluetooth LE HID | Battery Service 180F / Battery Level 2A19 | Implementat; verificarea fizică pe tastatură rămâne de făcut. |
| Alte căști wireless din catalog | HeadsetControl, în funcție de model | Implementat; fiecare model necesită verificare. |
| Receiver Pulsar de tastatură F809 | Identificat wireless | Protocolul bateriei nu este suportat. |
| Dispozitive USB necunoscute | Nu se presupune un protocol proprietar | Neclasificate/nesuportate; pagina Compatibilitate. |
| Periferice exclusiv cu fir | Excluse | Nu sunt monitorizate. |

Modelele wireless cunoscute pot rămâne afișate prin cablu USB. Încărcarea suspendă alerta numai când este raportată explicit. Interfețele sunt unite numai după o dovadă de identitate fizică; VID/PID sau numele modelului nu sunt suficiente. Datele deconectate sau vechi nu devin zero. Dacă BLE expune mai multe baterii valide, se folosește cel mai mic procent.

Scenarii fizice restante: PRO X Wireless, CrazyLight, o tastatură wireless, receiver Logitech cu mai multe periferice, încărcare, reconectare, repaus și jocuri pe tot ecranul. Primirea pe telefon blocat necesită confirmare după configurarea personală ntfy.

## References / Referințe

- [HeadsetControl 4.1.0](https://github.com/Sapd/HeadsetControl/releases/tag/4.1.0)
- [Official Pulsar cMouse configuration / Configurația oficială](https://bbb.pulsar.gg/cMouse/cfg.json), snapshot 1.3.12.
- [Logitech HID++ specifications](https://github.com/Logitech/cpg-docs/tree/master/hidpp20) and [Solaar device documentation](https://pwr-solaar.github.io/Solaar/devices/).
- [Microsoft BLE Battery Level](https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile.gattcharacteristicuuids.batterylevel).
