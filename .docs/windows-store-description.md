# Windows (Microsoft Store) store listing text

Use these in **Partner Center** (or the Store submission flow) for the Windows app listing.

---

## Restricted capability: runFullTrust (approval justification)

When Partner Center asks why the app uses the **runFullTrust** capability, use the text below in the justification field.

**Why do you need the runFullTrust capability, and how will it be used in your product?**

```
Desktop alarm app (WinUI 3/MAUI). runFullTrust is required for: (1) Local SQLite databases and cached audio files for offline playback—needs full file access. (2) Recurring alarm scheduling and a background task that keeps notifications scheduled and pre-downloads media. (3) Background audio playback when the window is minimized. No sensitive OS access or extra data collection; used only for local storage, scheduling, and playback.
```

---

## Short description (270 characters or fewer)

Used at the top of your product's Store listing. Keep it catchy and concise.

```
Publication audio from jw.org. Set Bible listening as an alarm or play on demand. Optional music, 240+ languages, stream or download. Free, no ads.
```
(118 characters)

Alternative:

```
Make your New World Translation Bible listening easy: alarms, on-demand playback, optional music, 240+ languages. Stream or download from jw.org. Free, no ads.
```
(127 characters)

---

## Description (Tell customers what your product does)

Make your New World Translation (NWT) Bible listening schedule easy: set it as an alarm or play on demand when you have time. Optionally start with music before the scheduled Bible reading. Chapters and verses resume where you left off.

Create one or more recurring alarms with your choice of Bible publication and language. You can stream audio or download it for offline playback. Use system media controls to play, pause, or skip. The app is non-commercial and free, with no advertisements.

Audio is available in over 240 languages, streamed or downloaded from jw.org. At present, only the New World Translation (NWT) is available for Bible reading.

This app is non-commercial and free, with no advertisements; the publisher does not gain and does not intend to gain anything monetarily. The app adheres to the terms of use for third-party apps as stated on jw.org (Jehovah's Witnesses).

---

## Product features (bulleted list)

Short summaries of your product's key features. Use as the Store’s “Product features” or “Key features” section.

- Recurring alarms with your choice of Bible publication and language
- Optional "begin with music" before the Bible reading
- Stream or download; playback continues in the background
- System media controls and lock-screen controls
- Resume by chapter and verse
- Over 240 languages available from jw.org
- No ads; non-commercial and free

---

## Optional: extended description with language list

If the Store allows a longer description and you want to include the full language list, use the same paragraph as in **Description** above, then add:

**Bible audio is available (partially or fully) in the following languages:**

Abkhaz, Acholi, Afrikaans, Aja, Albanian, Altai, Alur, Amharic, Armenian, Aukan, Aymara, Azerbaijani, Bashkir, Basque, Bassa (Cameroon), Batak (Karo), Batak (Toba), Belize Kriol, Bengali, Bicol, Bulgarian, Cambodian, Catalan, Changana (Mozambique), Chichewa, Chin (Hakha), Chinese Cantonese (Simplified), Chinese Cantonese (Traditional), Chinese Mandarin (Simplified), Chinese Mandarin (Traditional), Chitonga (Malawi), Chitonga (Zimbabwe), Chitumbuka, Chiyao, Chol, Chopi, Chuabo, Chuvash, Cibemba, Cinamwanga, Cinyanja, Croatian, Czech, Dangme, Danish, Dutch, Edo, English, Estonian, Ewe, Fante, Finnish, Fon, French, Ga, Galician, Garifuna, Georgian, German, Greek, Guadeloupean Creole, Guarani, Gujarati, Gun, Haitian Creole, Hausa, Hebrew, Herero, Hiligaynon, Hindi, Hindi (Roman), Hmong (White), Hungarian, Iban, Igbo, Iloko, Indonesian, Isoko, Italian, Japanese, Javanese, Kabiye, Kabuverdianu, Kamba, Kannada, Kanyok, Karen (S'gaw), Kazakh, Kazakh (Arabic), Kekchi, Kikuyu, Kiluba, Kinande, Kinyarwanda, Kirghiz, Kirundi, Kisi, Kisonge, Kituba, Kokola, Kongo, Konkani (Roman), Korean, Kwangali, Kwanyama, Lari, Lendu, Lhukonzo, Liberian English, Lingala, Lithuanian, Lolo, Lomwe, Low German, Luganda, Luo, Macedonian, Macua, Makhuwa-Meetto, Makhuwa-Shirima, Malagasy, Malay, Malayalam, Maltese, Mam, Manyawa, Mapudungun, Marathi, Mauritian Creole, Maya, Mbunda, Mingrelian, Mixe (North Central), Mixtec (Guerrero), Mizo, Moore, Motu, Myanmar, Nahuatl (Central), Nahuatl (Guerrero), Nahuatl (Huasteca), Nahuatl (Northern Puebla), Ndau, Ndebele, Ndebele (Zimbabwe), Ndonga, Nepali, Ngabere, Nias, Nicobarese, Norwegian, Nsenga (Mozambique), Nsenga (Zambia), Nyaneka, Nyungwe, Nzema, Oromo, Otetela, Papiamento (Curaçao), Persian, Phimbi, Polish, Portuguese (Brazil), Portuguese (Portugal), Punjabi, Punjabi (Roman), Punjabi (Shahmukhi), Quechua (Ancash), Quechua (Ayacucho), Quechua (Bolivia), Quechua (Cuzco), Quechua (Huallaga Huánuco), Quiche, Quichua (Chimborazo), Quichua (Imbabura), Romanian, Romany (Eastern Slovakia), Romany (Macedonia), Romany (Macedonia) Cyrillic, Romany (Southern Greece), Ronga, Russian, Samoan, Saramaccan, Sena, Sepedi, Sepulana, Serbian (Cyrillic), Serbian (Roman), Sesotho (Lesotho), Sesotho (South Africa), Setswana, Seychelles Creole, Shona, Sidama, Sinhala, Slovak, Slovenian, Spanish, Sranantongo, Sunda, Swahili, Swahili (Congo), Swati, Swedish, Taabwa, Tagalog, Tahitian, Tamil, Tamil (Roman), Tandroy, Tankarana, Tarascan, Tatar, Telugu, Tetun Dili, Tewe, Thai, Tigrinya, Tlapanec, Tojolabal, Tongan, Totonac, Toupouri, Tshiluba, Tshwa, Tsonga, Turkish, Turkmen, Twi, Tzeltal, Tzotzil, Ukrainian, Urdu, Uruund, Uzbek, Valencian, Venda, Vezo, Vietnamese, Wallisian, Wayuunaiki, Wolaita, Xhosa, Yacouba, Yoruba, Zapotec (Isthmus), Zulu.

To refresh the language list from the media index: `dotnet run --project .tools/Bible.Alarm.DbMigration -- list-bible-languages`
