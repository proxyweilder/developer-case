# Teknik kararlar

.NET 8, Dapper ve parametreli SQL seçildi: mevcut şemayı korumak ve kilit/transaction sırasını açık göstermek amaçlandı. Her case bağımsız API, Domain, test ve veritabanına sahiptir. UI, yetkilendirme ve üretim dağıtımı kapsam dışıdır.

**Case 1:** Sıralı ürün okumalarında UPDLOCK/HOLDLOCK, transaction ve CHECK kısıtları stok güvenliğini korur; yazmalar topludur. Deadlock/zaman aşımı geri alınır, 503 döner. Madde 2 yorumu: tumunuReddet bütün isteği reddeder; uygunMiktariAl her satırın mevcut miktarını ayırır, tümü sıfırsa sipariş oluşmaz. Madde 3 yorumu: zorunlu Idempotency-Key, anahtar kilidi, gövde özeti ve benzersiz indeks tekillik sağlar; aynı içerik aynı kimlik/güncel durum, farklı içerik 409 döndürür. İptal kalan rezervasyonu aşamaz; çok satırlı hata bütünü geri alır.

**Case 2 eski kod incelemesi:**

| Sorun | Karar ve gerekçe |
|---|---|
| SQL'e metin ekleme, yerel sayı biçimi | Düzeltildi: parametreler enjeksiyonu ve kültür hatasını önler. |
| Erken çıkış/hata sonrası açık bağlantı/reader | Düzeltildi: await using kaynakları bırakır. |
| Teklif ve bakiye ayrı yazılıyor | Düzeltildi: tek transaction atomiklik sağlar. |
| Paralel isteklerde eski bakiye | Düzeltildi: cari transaction boyunca güncelleme kilidiyle okunur. |
| >=100 iskonto dalı ulaşılamıyor | Korundu/sorulmalı: sonuç değişmesin diye %5; gerçek oran teyit edilmeli. |
| BAYI'ye 3 puan ekleme, diğer gruplar | Korundu: mevcut eşleşme sürer; bilinmeyen grup politikası sorulmalı. |
| Satır yuvarlama ve sabit %20 KDV | Korundu: ToEven ve mevcut oran testlerle sabitlendi. |
| Kredi kontrolünde yuvarlanmamış tutar | Korundu: eski karşılaştırma ve DECIMAL(18,2) saklama sürer. |
| Miktar/fiyat/boş satır kontrolü yok | Düzeltildi: doğrulama eklendi; negatif fiyat politikası sorulmalı. |
| Arayüze bağımlılık, boş DataGridView satırı | Düzeltildi: tipli JSON ve bağımsız hesaplama kullanılır. |
| MsgBox onayı | Düzeltildi: 409 sonrası açık bool onay; güncel bakiye tekrar okunur. |
| Teklifin bakiye artırması | Korundu/sorulmalı: teklifin borç yaratması iş kararıdır. |
| Mevcut kolonlar ve tarih | Korundu: şema değiştirilmedi, GETDATE kullanılır. |
| Desktop transaction kullanmıyor | Sorulmalı: ortak güvenlik için eski yazma akışı da düzeltilmeli. |
| Teklif tekrar gönderimi | Korundu/sorulmalı: Case 2 tekillik istemiyor; her başarı yeni teklif. |

**Durum:** Derleme başarılı; güncel SQL çalıştırmasında 27 birim ve 16 entegrasyon testi dahil 43/43 test geçti. İki runtime container da açıldı; 5081/5082 sağlık kontrolleri HTTP 200 / Healthy döndü. AI dökümü, gerçek süre kaydı ve teslim tarihi henüz tamamlanmadı; 3 saat sınırına uyulduğu varsayılmadı. Ek sürede yük/SQL planı ölçümü, migration hesabı ve desktop ile güvenli ortak yazma ele alınır. Ayrıntılar docs/SORGULAR.md içindedir.
