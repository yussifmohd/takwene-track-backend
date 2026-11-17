# Data Model: Sample catalogue (no schema change)

No new tables or columns; no migration. The sample is ordinary rows created through the domain.

## Artists (17) – emails `<slug>@example.com`

| # | Name (as stored) | Script | Country |
|---|---|---|---|
| 1 | Amr Diab | Latin | EG |
| 2 | Wegz | Latin | EG |
| 3 | حسن شاكوش (Hassan Shakosh) | Arabic | EG |
| 4 | Cairokee | Latin | EG |
| 5 | نانسي عجرم (Nancy Ajram) | Arabic | LB |
| 6 | Elissa | Latin | LB |
| 7 | حسين الجسمي (Hussain Al Jassmi) | Arabic | AE |
| 8 | محمد عبده (Mohammed Abdu) | Arabic | SA |
| 9 | Saint Levant | Latin | PS |
| 10 | The Beatles | Latin | GB |
| 11 | Led Zeppelin | Latin | GB |
| 12 | Queen | Latin | GB |
| 13 | The Prodigy | Latin | GB |
| 14 | Michael Jackson | Latin | US |
| 15 | Whitney Houston | Latin | US |
| 16 | Eminem | Latin | US |
| 17 | The Notorious B.I.G. | Latin | US |

(The Latin name in brackets is only for this table; Arabic-script artists are stored in Arabic.)

## Tracks (32) – ISRC `XX-SMP-26-900NN`, release dates invented (2026)

Deliveries: S = Spotify, A = Apple Music, Y = YouTube Music; p = pending, L = live, R = rejected,
p* = rejected then sent again (pending with a later send time).

| # | Artist | Title | Genre | Status | Deliveries |
|---|---|---|---|---|---|
| 1 | Amr Diab | Tamally Maak | Arabic Pop | distributed | S L, A L, Y L |
| 2 | Amr Diab | Nour El Ein | Arabic Pop | submitted | – |
| 3 | Amr Diab | Wayah | Arabic Pop | distributed | A p |
| 4 | Wegz | El Bakht | Rap | distributed | S L, Y p |
| 5 | Wegz | Dorak Gai | Rap | draft | – |
| 6 | حسن شاكوش | بنت الجيران | Mahraganat | distributed | Y L, S R |
| 7 | Cairokee | Ya El Midan | Rock | submitted | – |
| 8 | نانسي عجرم | آه ونص | Arabic Pop | distributed | S L, A L, Y R |
| 9 | نانسي عجرم | Ya Tabtab | Arabic Pop | draft | – |
| 10 | Elissa | Aa Bali Habibi | Arabic Pop | submitted | – |
| 11 | Elissa | Asaad Wahda | Arabic Pop | draft | – |
| 12 | حسين الجسمي | بشرة خير | Khaleeji | distributed | S L, A L |
| 13 | حسين الجسمي | Ahebak Moot | Khaleeji | submitted | – |
| 14 | محمد عبده | الأماكن | Khaleeji | distributed | Y R, S p |
| 15 | Saint Levant | Very Few Friends | Hip-Hop | distributed | S L |
| 16 | Saint Levant | Exile | Hip-Hop | draft | – |
| 17 | The Beatles | Hey Jude | Rock | distributed | S L, A L, Y L |
| 18 | The Beatles | Let It Be | Rock | submitted | – |
| 19 | The Beatles | Yesterday | Rock | draft | – |
| 20 | Led Zeppelin | Stairway to Heaven | Rock | distributed | S L, A p* |
| 21 | Led Zeppelin | Whole Lotta Love | Rock | submitted | – |
| 22 | Michael Jackson | Billie Jean | R&B | distributed | S L, A L, Y p |
| 23 | Michael Jackson | Thriller | R&B | draft | – |
| 24 | Queen | Bohemian Rhapsody | Rock | distributed | Y R |
| 25 | Queen | Don't Stop Me Now | Rock | submitted | – |
| 26 | Whitney Houston | I Will Always Love You | R&B | distributed | A R, S L |
| 27 | Eminem | Lose Yourself | Rap | distributed | S p, A p, Y p |
| 28 | Eminem | Without Me | Rap | draft | – |
| 29 | The Prodigy | Firestarter | Electronic | distributed | S p, Y L |
| 30 | The Prodigy | Breathe | Electronic | submitted | – |
| 31 | The Notorious B.I.G. | Juicy | Hip-Hop | submitted | – |
| 32 | The Notorious B.I.G. | Big Poppa | Hip-Hop | draft | – |

Coverage: draft 8, submitted 9, distributed 15; every genre ≥ 1 (Mahraganat 1, Electronic 2);
pending, live and rejected each occur on Spotify, Apple Music and YouTube Music; one re-send (#20).

## How each row is built (domain only)
- draft: `Track.Create`.
- submitted: `Create` → `ChangeStatus(Submitted)`.
- distributed: `Create` → `ChangeStatus(Submitted)` → `Distribute(dsps, sentAt)` →
  `RecordAnswer(dsp, Live|Rejected, answeredAt > sentAt)` per answered delivery; for p* `Distribute([A])`
  again after the rejection.
