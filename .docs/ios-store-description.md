# iOS (App Store) store listing text

Use these in **App Store Connect** for the iOS app. All fields are **plain text** (no HTML, no bold). Line breaks are supported and will display on the store.

Language list source: media index DB (Bible category). To refresh: `dotnet run --project .tools/Bible.Alarm.DbMigration -- list-bible-languages`

---

## Promotional Text (170 characters max) — *optional*

Appears at the top of the description. You can change it without submitting a new version. **You can leave this blank**; Apple does not require it.

**Recommended (benefit-led):**

```
Wake up to the Bible—set your alarm, pick your language, and start the day with JW.org audio. CarPlay, 240+ languages, optional music. Free, no ads.
```
(128 characters)

```
Start your day with Bible listening. Set a recurring alarm, stream or download, and play in the car with CarPlay. 240+ languages. Free, no ads.
```
(118 characters)

**Shorter options:**

```
JW.org Bible & music alarms. CarPlay support. Stream or download. Free, no ads.
```
(68 characters)

```
Make your Bible listening an alarm. CarPlay, 240+ languages, optional music. Free, no ads.
```
(72 characters)

---

## Description (4000 characters max)

Plain text only; line breaks are preserved. Use the block below in App Store Connect → App Information → Description.

```
Make your New World Translation (NWT) Bible listening schedule easy: set it as an alarm or play on demand when you have time. Optionally start with an orchestra or a hymn before the scheduled Bible reading. Chapters and verses resume where you left off.

Features:
• Recurring alarms with your choice of Bible publication and language
• Optional "begin with music" (Kingdom Melodies or vocal music)
• Stream or download; playback continues in the background
• CarPlay support: play and control Bible and music from your car
• System media controls and lock-screen controls
• Resume by chapter and verse
• No ads; non-commercial and free

Audio is available in over 240 languages, streamed or downloaded from jw.org. At present, only the New World Translation (NWT) is available for Bible reading.

This app is non-commercial and free, with no advertisements; the publisher does not gain and does not intend to gain anything monetarily. The app adheres to the terms of use for third-party apps as stated on jw.org (Jehovah's Witnesses).

Bible audio is available (partially or fully) in the following languages:

Abkhaz, Acholi, Afrikaans, Aja, Albanian, Altai, Alur, Amharic, Armenian, Aukan, Aymara, Azerbaijani, Bashkir, Basque, Bassa (Cameroon), Batak (Karo), Batak (Toba), Belize Kriol, Bengali, Bicol, Bulgarian, Cambodian, Catalan, Changana (Mozambique), Chichewa, Chin (Hakha), Chinese Cantonese (Simplified), Chinese Cantonese (Traditional), Chinese Mandarin (Simplified), Chinese Mandarin (Traditional), Chitonga (Malawi), Chitonga (Zimbabwe), Chitumbuka, Chiyao, Chol, Chopi, Chuabo, Chuvash, Cibemba, Cinamwanga, Cinyanja, Croatian, Czech, Dangme, Danish, Dutch, Edo, English, Estonian, Ewe, Fante, Finnish, Fon, French, Ga, Galician, Garifuna, Georgian, German, Greek, Guadeloupean Creole, Guarani, Gujarati, Gun, Haitian Creole, Hausa, Hebrew, Herero, Hiligaynon, Hindi, Hindi (Roman), Hmong (White), Hungarian, Iban, Igbo, Iloko, Indonesian, Isoko, Italian, Japanese, Javanese, Kabiye, Kabuverdianu, Kamba, Kannada, Kanyok, Karen (S'gaw), Kazakh, Kazakh (Arabic), Kekchi, Kikuyu, Kiluba, Kinande, Kinyarwanda, Kirghiz, Kirundi, Kisi, Kisonge, Kituba, Kokola, Kongo, Konkani (Roman), Korean, Kwangali, Kwanyama, Lari, Lendu, Lhukonzo, Liberian English, Lingala, Lithuanian, Lolo, Lomwe, Low German, Luganda, Luo, Macedonian, Macua, Makhuwa-Meetto, Makhuwa-Shirima, Malagasy, Malay, Malayalam, Maltese, Mam, Manyawa, Mapudungun, Marathi, Mauritian Creole, Maya, Mbunda, Mingrelian, Mixe (North Central), Mixtec (Guerrero), Mizo, Moore, Motu, Myanmar, Nahuatl (Central), Nahuatl (Guerrero), Nahuatl (Huasteca), Nahuatl (Northern Puebla), Ndau, Ndebele, Ndebele (Zimbabwe), Ndonga, Nepali, Ngabere, Nias, Nicobarese, Norwegian, Nsenga (Mozambique), Nsenga (Zambia), Nyaneka, Nyungwe, Nzema, Oromo, Otetela, Papiamento (Curaçao), Persian, Phimbi, Polish, Portuguese (Brazil), Portuguese (Portugal), Punjabi, Punjabi (Roman), Punjabi (Shahmukhi), Quechua (Ancash), Quechua (Ayacucho), Quechua (Bolivia), Quechua (Cuzco), Quechua (Huallaga Huánuco), Quiche, Quichua (Chimborazo), Quichua (Imbabura), Romanian, Romany (Eastern Slovakia), Romany (Macedonia), Romany (Macedonia) Cyrillic, Romany (Southern Greece), Ronga, Russian, Samoan, Saramaccan, Sena, Sepedi, Sepulana, Serbian (Cyrillic), Serbian (Roman), Sesotho (Lesotho), Sesotho (South Africa), Setswana, Seychelles Creole, Shona, Sidama, Sinhala, Slovak, Slovenian, Spanish, Sranantongo, Sunda, Swahili, Swahili (Congo), Swati, Swedish, Taabwa, Tagalog, Tahitian, Tamil, Tamil (Roman), Tandroy, Tankarana, Tarascan, Tatar, Telugu, Tetun Dili, Tewe, Thai, Tigrinya, Tlapanec, Tojolabal, Tongan, Totonac, Toupouri, Tshiluba, Tshwa, Tsonga, Turkish, Turkmen, Twi, Tzeltal, Tzotzil, Ukrainian, Urdu, Uruund, Uzbek, Valencian, Venda, Vezo, Vietnamese, Wallisian, Wayuunaiki, Wolaita, Xhosa, Yacouba, Yoruba, Zapotec (Isthmus), Zulu.
```

If the full description exceeds 4000 characters, use a shorter version: keep the first three paragraphs and the disclaimer, and replace the language list with: "Bible audio is available (partially or fully) in over 240 languages, including Albanian, Amharic, Armenian, Bengali, Chinese (Mandarin and Cantonese), English, French, German, Hindi, Indonesian, Italian, Japanese, Korean, Portuguese (Brazil and Portugal), Russian, Spanish, Tagalog, Tamil, and many more."

---

## Notes for Review (App Review information)

Shown only to App Review. Use to explain how to test the app and any relevant context. Adjust per submission.

```
Test account: Not required. The app works fully without login.

Content source: All audio (Bible and music) is streamed or downloaded from the public jw.org (Jehovah's Witnesses) website. No in-app purchases or subscriptions.

How to test:
- Alarms: Create a schedule (e.g. Bible → language → publication → section), set time and days, enable. Alarm fires at the set time (use a near-future time for quick testing). Notifications must be allowed.
- CarPlay: Connect a CarPlay simulator or device; the app appears under Media. Schedules and now playing are available. No login required.
- Playback: From Home, tap a schedule to play on demand. Stream or download; lock screen and Control Center show now playing. Background audio continues when app is in background.

The app is non-commercial, free, and ad-free; it adheres to jw.org terms of use for third-party apps.
```

---

## What's New in This Version

Shown to users when they view the app or updates. Edit for each release (typically 1–4 short items). Keep under 4000 characters.

**Template (customize per version):**

```
• CarPlay support: play your Bible and music schedules from your car
• Reliability and performance improvements
• Bug fixes
```

**Example (more detailed):**

```
• CarPlay: Access your Bible and music schedules and control playback from CarPlay
• Improved alarm scheduling and notification handling
• Playback and download stability improvements
• Bug fixes and performance updates
```

---

## Regenerating the language list

To refresh the Bible language list from the current media index:

```bash
dotnet run --project .tools/Bible.Alarm.DbMigration -- list-bible-languages
```

Optional: pass a path to the folder containing `index.zip` or `mediaIndex.db`:

```bash
dotnet run --project .tools/Bible.Alarm.DbMigration -- list-bible-languages "c:\path\to\.tools\_index"
```
