# Mail ve case dokümanı gereksinim incelemesi

Kaynak: kullanıcının paylaştığı mail metni ve Yazilim_Gelistirici_Case.docx. İnceleme 26 Ekim 2026 tarihinde güncel kaynak kod, testler ve çalışma kanıtları üzerinde yapıldı. GitHub'a yayın yapılmadı.

## Sonuç

İncelenen kapsamda çalışmayı engelleyen bir işlevsel kusur bulunmadı. Temel teknik gereksinimler karşılanıyor. Teslim henüz bütünüyle tamamlanmış değildir: ham AI dökümü eksik, gerçek çalışma süresi ve mail tarihi doğrulanmadı, KARARLAR.md için bir sayfalık baskı görünümü ayrıca doğrulanmalıdır.

## Genel gereksinimler

| Gereksinim | Sonuç | Kanıt |
|---|---|---|
| C# / ASP.NET Core / .NET 8+ | Karşılanıyor | İki API net8.0 |
| SQL Server ve açıklanan veri erişim seçimi | Karşılanıyor | Compose SQL Server 2022, Dapper; KARARLAR.md |
| Birbirinden bağımsız iki case | Karşılanıyor | Ayrı API, Domain, test ve veritabanı; cross-case proje referansı yok |
| Her iki case için tek repo/paket | Yapı hazır | DeveloperCase.sln ve kaynak ZIP'i; GitHub yüklemesi kullanıcı isteğiyle bekliyor |
| Tek komutla Docker Compose | Doğrulandı | İki runtime image açıldı, 5081/5082 sağlık kontrolleri 200/Healthy |
| Kritik kuralların otomatik testleri | Doğrulandı | Güncel 43 testin tamamı gerçek SQL erişimiyle geçti |
| README ve örnek istekler | Mevcut | README.md, requests/case1.http, requests/case2.http |
| KARARLAR içerik gereksinimleri | Mevcut | Seçim, varsayım, kapsam dışı, ek süre ve eski kod kararları yazılı |
| KARARLAR en fazla bir sayfa | Görsel kontrol bekliyor | Markdown'ın baskı sayfa sayısı seçilen görüntüleyiciye bağlı |
| Düzenlenmemiş AI görüşme dökümü | Eksik | AI_KONUSMA_DOKUMU.md bir durum notu; gerçek döküm değil |
| En fazla 3 saat çalışma | Doğrulanamadı | Gerçek çalışma süresi kaydedilmeli; sonradan varsayılmamalı |
| Mailden itibaren 1 hafta teslim | Doğrulanamadı | Mailin gönderim tarihi paylaşılmadı |

## Case 1

| Gereksinim | Sonuç / uygulama |
|---|---|
| En az üç başlangıç ürünü ve stok | URN-001=10, URN-002=1, URN-003=0; yeniden açılış mevcut stoğu sıfırlamaz |
| POST /siparisler | Mevcut; stok rezervasyonu ve sipariş aynı transaction'da |
| POST /siparisler/{id}/iptal | Mevcut; tam ve kısmi iptal |
| GET /siparisler/{id} | Mevcut; durum ve tüm satırlar |
| GET /urunler/{kod}/stok | Mevcut; fiziksel, rezerve ve kullanılabilir miktar |
| Kullanılabilir stok negatif olmaz | Sıralı UPDLOCK/HOLDLOCK okumaları, transaction ve CHECK kısıtı |
| Son bir ürüne paralel iki sipariş | Gerçek SQL testi: yalnız bir 201, bir 409 ve rezerve miktar 1 |
| Yetersiz stok müşteri tercihi | tumunuReddet veya uygunMiktariAl; yorum karar dosyasında |
| İstemci tekrarı ikinci sipariş oluşturmaz | Anahtar kilidi, gövde özeti, benzersiz indeks ve paralel tekrar testi |
| Çift/fazla iptal önlenir | Aktif rezervasyon kontrolü, aynı siparişe kilit ve SQL kısıtı |
| Olmayan ürün, sıfır/negatif miktar | Açıklamalı 400/404; otomatik ve canlı kullanım kontrolleri |
| Şema | schema.sql mevcut |

Eksik bir zorunlu endpoint veya iş kuralı tespit edilmedi. Çok satırlı yetersiz stokta bütün isteğin reddini doğrulayan SQL testi eklendi. İptal rollback testinde sipariş satırları yanında ürün stokları da artık doğrulanıyor.

## Case 2

| Gereksinim | Sonuç / uygulama |
|---|---|
| POST /teklifler | Mevcut; cari kod, miktar ve birim fiyat alır |
| İş kuralları ayrı katmanda | QuoteRules Domain içinde; HTTP ve SQL bağımlılığı yok |
| Mevcut Cariler/Teklifler şeması korunur | Dokümandaki kolon/tipler kullanılır; mevcut tablolara ALTER/DROP uygulanmaz |
| Iskonto / KDV / kredi kontrolü | Eski davranışla uyumlu hesaplama ve sınır testleri |
| Devam edilsin mi onayı | Önce 409 ve ayrıntılar; açık bool onayla yeniden hesaplama/kayıt |
| Eski >=100 iskonto kusuruna karar | Bilerek %5 korunuyor; %10'a sessizce düzeltilmedi; soru olarak belgeli |
| Mevcut sonuçları sabitleyen testler | Bayi/perakende 49/50/99/100/101 sınırları ve satır yuvarlaması |
| Sorun bazında korundu/düzeltildi/sorulmalı | KARARLAR.md tablosunda mevcut |
| Teklif ve bakiye atomikliği | Transaction; yeni hata enjeksiyonu testi bakiye yazımı bozulursa teklifin de geri alındığını gösteriyor |

Eski masaüstü uygulaması transaction dışında kredi kontrolü yapmaya devam ederse API tek başına onun eskimiş kararını engelleyemez. Bu bir API test başarısızlığı değildir; geçiş koşulu KARARLAR.md ve sorgu rehberinde sorulmalı olarak belirtilmiştir.

## Bu incelemede yapılan iyileştirmeler

1. README'deki yerel SQL bağlantı örneği tcp:127.0.0.1,14333 olarak düzeltildi. Bu ortamda localhost bağlantısı zaman aşımı verdi; açık IPv4 adresiyle bağlantı ve testler başarılı oldu. Container içindeki Server=sql bağlantısı etkilenmedi.
2. Case 1 çok satırlı yetersiz stokta hiçbir stok/sipariş yazılmadığını doğrulayan entegrasyon testi eklendi.
3. Case 1 başarısız çok satırlı iptalde stokların da geri alındığı doğrulandı.
4. Case 2 bakiye yazma hatasında teklifin de geri alındığını doğrulayan entegrasyon testi eklendi. Hata tetikleyicisi yalnız ayrı test veritabanında kuruldu ve kaldırıldı.

Güncel sonuç: Case 1 21/21, Case 2 22/22; toplam 43 başarılı, 0 başarısız, 0 atlanan. Son kanıt sql-reliability-test-run.txt içindedir; önceki 39 testlik çıktı review-test-run.txt içinde korunmuştur. Önceki Docker 37/37 çıktısı test-run.txt içinde tarihsel kanıt olarak korunmuştur.

## SQL Server ek doğrulamaları

Doğrudan SQL CHECK kısıtları, başlangıç scriptinin tekrar çalıştırılması, eski kolonların metadata sözleşmesi ve para değerlerinin saklanması için dört entegrasyon testi eklendi ve geçti. Kodun transaction düzeni değişmedi. Hatalı rezervasyon yazmaları SQL Server tarafından reddedildi; tekrar başlangıç veriyi sıfırlamadı; eski Cariler/Teklifler kolonları ve kayıtlar korundu; API para değerleri SQL kayıtlarıyla eşleşti.

## Zorunlu olmayan iyileştirmeler

- Beklenen RuleViolation durumları .NET 8 middleware'inde Error/fail olarak loglanıyor. HTTP yanıtları doğru; log seviyeleri daha anlaşılır hale getirilebilir.
- Gerçek yük ve SQL yürütme planı ölçümü yapılmadı; mevcut testler doğruluk testi. Ölçülmüş kapasite iddiası yok.
- Mevcut müşteri veritabanında desktop ile birlikte uçtan uca geçiş testi yapılmadı; sağlanan şema ve bağımsız SQL testleri kullanıldı.

UI, ürün kartları, sepet, kullanıcı girişi, ekstra endpoint'ler veya ayrıca bilgisayar parçası kataloğu dokümanın zorunlu teslimleri değildir. Bu öğelerin bulunmaması eksiklik olarak değerlendirilmedi.
