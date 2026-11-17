# Data Model: Sample catalogue (no schema change)

No new tables or columns; no migration. The sample is ordinary rows created through the domain.

**True** (checked 2026-11-17, sources below): artist names, countries, song titles, the artist–song
pairing, and the genre (chosen from the fixed list only where sources describe the song that way).
**Invented sample data**: every ISRC (`XX-SMP-26-9NNNN`), email (`@example.com`), release date, status,
delivery and timestamp. Nothing here claims to be a real catalogue record, contact or chart fact.

## Artists (17)

| # | Name (as stored) | Country | Source for country / identity |
|---|---|---|---|
| 1 | Amr Diab | EG | Wikipedia "Amr Diab" |
| 2 | Wegz | EG | Egyptian Streets, Wikipedia "Wegz" |
| 3 | حسن شاكوش (Hassan Shakosh) | EG | Cairo Scene, Forbes Middle East |
| 4 | Cairokee | EG | Wikipedia "Cairokee" (Egyptian rock band, Cairo, 2003) |
| 5 | نانسي عجرم (Nancy Ajram) | LB | Wikipedia "Nancy Ajram" |
| 6 | Elissa | LB | Wikipedia "Elissa (singer)" |
| 7 | حسين الجسمي (Hussain Al Jassmi) | AE | Wikipedia "Hussain Al Jasmi" (Emirati) |
| 8 | محمد عبده (Mohammed Abdu) | SA | Wikipedia "Mohammed Abdu", Saudipedia, Arab News |
| 9 | Saint Levant | PS | Rolling Stone, Wikipedia "Saint Levant" (Palestinian) |
| 10 | The Beatles | GB | (well-known; Liverpool) |
| 11 | Led Zeppelin | GB | (well-known; London) |
| 12 | Queen | GB | (well-known; London) |
| 13 | The Prodigy | GB | Wikipedia "The Prodigy" |
| 14 | Michael Jackson | US | Wikipedia "Billie Jean" |
| 15 | Whitney Houston | US | Songfacts |
| 16 | Eminem | US | Grammy.com |
| 17 | The Notorious B.I.G. | US | American Songwriter |

Arabic-script artists are stored in Arabic; the Latin name in brackets is only for this table.
Emails: `<slug>@example.com` (invented).

## Tracks (32) – ISRC `XX-SMP-26-900NN`, release dates invented (2026)

Deliveries: S = Spotify, A = Apple Music, Y = YouTube Music; p = pending, L = live, R = rejected,
p* = rejected then sent again (pending with a later send time).

| # | Artist | Title | Genre | Status | Deliveries | Pairing / genre source |
|---|---|---|---|---|---|---|
| 1 | Amr Diab | Tamally Maak | Arabic Pop | distributed | S L, A L, Y L | Wikipedia; Rolling Stone "Best Arabic Pop Songs of the 21st Century" (2000) |
| 2 | Amr Diab | Nour El Ein | Arabic Pop | submitted | – | Wikipedia "Amr Diab" (1996) |
| 3 | Amr Diab | Wayah | Arabic Pop | distributed | A p | Wikipedia "Amr Diab" (2009, African Music Awards best song) |
| 4 | Wegz | El Bakht | Rap | distributed | S L, Y p | Cairo Scene, Scoop Empire |
| 5 | Wegz | Dorak Gai | Rap | draft | – | Egyptian Streets, Elle Egypt (2020) |
| 6 | حسن شاكوش | بنت الجيران | Mahraganat | distributed | Y L, S R | Cairo Scene, Ahram Online (2020 mahraganat hit, feat. Omar Kamal) |
| 7 | Cairokee | Ya El Midan | Rock | submitted | – | The National, Wikipedia "Cairokee" (2011, with Aida El Ayouby) |
| 8 | نانسي عجرم | آه ونص | Arabic Pop | distributed | S L, A L, Y R | Wikipedia "Ah W Noss" (2004) |
| 9 | نانسي عجرم | Ya Tabtab | Arabic Pop | draft | – | Wikipedia "Ya Tabtab...Wa Dallaa" (2006) |
| 10 | Elissa | Aa Bali Habibi | Arabic Pop | submitted | – | Wikipedia "Tesada'a Bemeen" (2009) |
| 11 | Elissa | Asaad Wahda | Arabic Pop | draft | – | Wikipedia "Asaad Wahda" (2012 title track) |
| 12 | حسين الجسمي | Ahebak | Khaleeji | distributed | S L, A L | Apple Music (2017 single; artist genre Khaleeji), Middle East Eye |
| 13 | محمد عبده | Ab'ad | Khaleeji | submitted | – | Arab News; Viberate (genre Khaleeji) |
| 14 | محمد عبده | الأماكن | Khaleeji | distributed | Y R, S p | Viberate, Platinumlist (album *Agrab Al Nass*, 2008) |
| 15 | Saint Levant | Very Few Friends | Rap | distributed | S L | Rolling Stone, Scene Noise (2022 trilingual rap) |
| 16 | محمد عبده | Al Rasayel | Khaleeji | draft | – | Arab News |
| 17 | The Beatles | Come Together | Rock | distributed | S L, A L, Y L | Louder, MusicRadar (blues rock) |
| 18 | The Beatles | Revolution | Rock | submitted | – | Ultimate Classic Rock, Cheat Sheet |
| 19 | The Beatles | Helter Skelter | Rock | draft | – | Wikipedia "Helter Skelter (song)" (hard rock) |
| 20 | Led Zeppelin | Stairway to Heaven | Rock | distributed | S L, A p* | Guitar World, Radio X (1971) |
| 21 | Led Zeppelin | Whole Lotta Love | Rock | submitted | – | Guitar World, Louder |
| 22 | Michael Jackson | Billie Jean | R&B | distributed | S L, A L, Y p | Wikipedia "Billie Jean" (genres incl. R&B) |
| 23 | Michael Jackson | Remember the Time | R&B | draft | – | Wikipedia "Remember the Time" (R&B, new jack swing) |
| 24 | Queen | Bohemian Rhapsody | Rock | distributed | Y R | American Songwriter (1975, *A Night at the Opera*) |
| 25 | Led Zeppelin | Black Dog | Rock | submitted | – | Guitar World "50 greatest Led Zeppelin songs" |
| 26 | Whitney Houston | I Will Always Love You | R&B | distributed | A R, S L | Songfacts (re-arranged as an R&B ballad, 1992) |
| 27 | Eminem | Lose Yourself | Rap | distributed | S p, A p, Y p | Grammy.com (first rap song to win the Oscar) |
| 28 | Eminem | Without Me | Rap | draft | – | NME |
| 29 | The Prodigy | Firestarter | Electronic | distributed | S p, Y L | Wikipedia "The Prodigy" (big beat), Number 1s Blog (1996) |
| 30 | The Prodigy | Breathe | Electronic | submitted | – | Wikipedia "Breathe (The Prodigy song)" |
| 31 | The Notorious B.I.G. | Juicy | Hip-Hop | submitted | – | American Songwriter (1994, *Ready to Die*) |
| 32 | The Notorious B.I.G. | Big Poppa | Hip-Hop | draft | – | American Songwriter |

Coverage: draft 8, submitted 9, distributed 15 (= 32); every genre ≥ 1 (Mahraganat 1, Electronic 2,
Hip-Hop 2); pending, live and rejected each occur on Spotify, Apple Music and YouTube Music; one re-send (#20).

### Changed after verification (2026-11-17)
| Draft pairing | Result | Replacement |
|---|---|---|
| Hussain Al Jassmi – "Ahebak Moot" | **not found**; the real song is "Ahebak" | "Ahebak" (#12) |
| Hussain Al Jassmi – "Boshret Kheir" (Khaleeji) | real, but an Egyptian song by Amr Mostafa – Khaleeji would be false | removed; Mohammed Abdu "Ab'ad" (#13) |
| Saint Levant – "Exile" | real (2025 single), but Raï-inspired – Hip-Hop would be false | Mohammed Abdu "Al Rasayel" (#16) |
| Wegz – "Dorak Gai", Elissa – "Asaad Wahda" | **confirmed** | kept |
| The Beatles – "Hey Jude", "Let It Be", "Yesterday" (Rock) | real, but usually described as pop rock / ballads | "Come Together", "Revolution", "Helter Skelter" |
| Michael Jackson – "Thriller" / "Beat It" | genre labels disputed (funk/post-disco; rock vs pop) | "Remember the Time" (#23) |
| Queen – "Don't Stop Me Now" / "We Will Rock You" | not confirmed in the search results | Led Zeppelin "Black Dog" (#25) |
| Saint Levant – "Very Few Friends" (Hip-Hop) | sources say "rap" | genre Rap (#15) |

### "Pop" genre – not added
The fixed genre list (Arabic Pop, Mahraganat, Khaleeji, Hip-Hop, Electronic, Rap, R&B, Rock) is the
developer's decision from the first interview. Every sample song was chosen so that its genre is true
within that list, so "Pop" is not needed. Adding it would change the domain list and validation, the
OpenAPI enum and committed contract, the generated web types, the genre dropdown and filter, and their
tests – a product change, not a data fix.

## How each row is built (domain only)
- draft: `Track.Create`.
- submitted: `Create` → `ChangeStatus(Submitted)`.
- distributed: `Create` → `ChangeStatus(Submitted)` → `Distribute(dsps, sentAt)` →
  `RecordAnswer(dsp, Live|Rejected, answeredAt > sentAt)` per answered delivery; for p* `Distribute([A])`
  again after the rejection.

## Sources (searched 2026-11-17)
- Wegz "Dorak Gai": https://egyptianstreets.com/2024/09/11/birthday-spotlight-how-wegz-left-a-mark-on-egyptian-hip-hop/ , https://www.elle.eg/elle-picks-top-5-wegz-music-videos/
- Wegz "El Bakht": https://cairoscene.com/Buzz/Wegz-s-Hit-Song-El-Bakht-Gets-184-2-Million-Views-on-YouTube
- Elissa "Asaad Wahda": https://en.wikipedia.org/wiki/Asaad_Wahda ; "Aa Bali Habibi": https://en.wikipedia.org/wiki/Tesada%27a_Bemeen
- Hussain Al Jassmi "Ahebak": https://tunebat.com/Info/Ahebak-Hussain-Aljassmi/3Fs5PAuGDSeQ9iA3FY38kT , https://www.middleeasteye.net/video/emirati-artists-song-performed-israeli-band , https://www.Wikipedia.org/wiki/Hussain_Al_Jasmi
- Saint Levant "Exile" (checked, not used): https://www.arabsounds.net/?p=3945 ; "Very Few Friends": https://www.rollingstone.com/music/music-features/saint-levant-very-few-friends-i-guess-1234650481/ , https://scenenoise.com/New-Music/Saint-Levant-is-in-His-Lover-Boy-Era-With-Very-Few-Friends
- Mohammed Abdu "Al Amaken", "Ab'ad", "Al Rasayel", Khaleeji: https://arabnews.com/node/2206256 , https://next.cluster.viberate.com/artist/mohammed-abdu/ , https://platinumlist.net/artist/79/mohammed-abdu
- Hassan Shakosh "Bint El Geeran": https://cairoscene.com/Noise/Hassan-Shakosh-To-Perform-Live-At-The-2022-FIFA-World-Cup-In-Qatar , https://english.ahram.org.eg/WorldCup/News/364158.aspx
- Nancy Ajram: https://en.wikipedia.org/wiki/Ya_Tabtab...Wa_Dallaa , https://Www.Wikipedia.org/wiki/Ah_W_Noss
- Amr Diab: https://www.wikipedia.org/wiki/Amr_Diab , https://www.arabsounds.net/?p=1559
- Cairokee "Ya El Midan": https://www.thenationalnews.com/arts-culture/music/cairokee-one-cairo-band-becomes-a-revolutionary-discovery-1.574717 , https://www.Wikipedia.org/wiki/Cairokee
- Michael Jackson: https://en.wikipedia.org/wiki/Billie_Jean , https://en.wikipedia.org/wiki/Remember_the_Time
- Whitney Houston: https://www.songfacts.com/facts/whitney-houston/i-will-always-love-you
- The Beatles: https://www.loudersound.com/features/12-songs-the-beatles-rocked , https://Www.wikipedia.org/wiki/Helter_Skelter_(song) , https://ultimateclassicrock.com/heaviest-beatles-songs/
- Led Zeppelin, Queen: https://www.guitarworld.com/magazine/50-greatest-led-zeppelin-songs , https://americansongwriter.com/7-iconic-rock-songs-from-the-1970s/
- The Prodigy: https://Wikipedia.com/wiki/The_Prodigy , https://www.wikipedia.org/wiki/Breathe_(Prodigy_song) , https://number1sblog.com/2024/02/17/736-firestarter-by-the-prodigy/
- Eminem: https://www.grammy.com/news/feature-why-eminems-lose-yourself-stands-tall-record , https://www.nme.com/lists/eminem-s-10-greatest-hits-1233
- The Notorious B.I.G.: https://americansongwriter.com/top-10-songs-by-the-notorious-b-i-g
