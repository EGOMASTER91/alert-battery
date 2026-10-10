# Design și imagini

Interfața WPF folosește grafit, accente verde-mentă, spațiere generoasă, carduri de produs, indicatoare de baterie și o pagină separată pentru alerte. Toate imaginile incluse sunt resurse locale; nu se fac descărcări în timpul monitorizării.

## Fotografii de produs

Fotografiile rămân proprietatea producătorilor și sunt folosite pentru identificarea vizuală în aplicație. Culoarea și dimensiunea nu sunt raportate de toate receiverele; imaginile sunt reprezentative. Butonul **Fotografie** permite înlocuirea lor cu propria imagine.

| Resursă | Sursă oficială |
| --- | --- |
| `logitech-pro-x2.png` | [Logitech PRO X 2](https://www.logitechg.com/en-us/shop/p/pro-x-2-wireless-headset), imagine `gallery-1-pro-x-2-lightspeed-gaming-headset-black.png` |
| `logitech-pro-x.png` | [Logitech PRO X Wireless](https://www.logitechg.com/en-us/products/gaming-audio/pro-x-wireless-headset.html), imagine `pro-wireless-headset-gallery-1.png` |
| `pulsar-xlite-v3.png` | [Pulsar Xlite V3](https://www.pulsar.gg/products/xlite-v3-large-gaming-mouse), imagine `PulsarXliteV3LargeGamingMouse_Black_001-123342.png` |
| `pulsar-xlite-crazylight.png` | [Pulsar Xlite CrazyLight](https://www.pulsar.gg/products/xlite-crazylight-medium-gaming-mouse), imagine `Pulsar-Xlite-CrazyLight_Black_Medium_01-medium_large.png` |
| `beats-powerbeats-pro.jpg` | [Beats Powerbeats Pro](https://www.beatsbydre.com/support/earbuds/powerbeats-pro), imagine `set-up-and-use-powerbeats-pro.jpg.small.2x.jpg` |

Asocierea este făcută după modelul raportat, fără presupunerea că un ID Pulsar comun desemnează un anumit mouse. Modelele fără o fotografie potrivită primesc o ilustrație generică. Fotografiile alese de utilizator sunt copiate în `%LOCALAPPDATA%/BatteryAlert/device-images`; selecția se persistă prin **Salvează setările**.

## Textură originală

Skill utilizat: `imagegen`, cu instrumentul încorporat `image_gen`. Rezultat: `src/BatteryAlert.App/Assets/ambient-glass.png`.

Prompt:

> Create an abstract background texture asset for a premium Windows desktop battery-monitor utility. Wide horizontal composition, no text, no icons, no devices, no interface mockup. Entire image extremely dark graphite charcoal (#10151b), rightmost third a very restrained cyan teal aurora-like soft reflection on smoked glass and fine satin grain, with a gentle curved glass ridge. Left two-thirds nearly flat dark to support readable white interface text. Elegant, quiet industrial product aesthetic, sophisticated and subtle rather than bright gaming neon. Pure abstract material texture. Output wide landscape.

Previzualizările din `artifacts/premium-*.png` folosesc date demonstrative; nu confirmă citirea bateriei CrazyLight.
