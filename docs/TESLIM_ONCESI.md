# Teslim öncesi inceleme

## Bağımsızlık

| Case | API | Domain | Veritabanı | Tablolar | HTTP portu |
|---|---|---|---|---|---|
| 1 | Case1.Api | Case1.Domain | OrdersCase | Products, Orders, OrderLines | 5081 |
| 2 | Case2.Api | Case2.Domain | QuotesCase | Cariler, Teklifler | 5082 |

Projeler arası referanslar XML üzerinden kontrol edildi. API ve test projeleri yalnız kendi case'ine referans veriyor; Domain projeleri SQL/HTTP paketi veya diğer case'e bağımlı değil. Kaynak kodda diğer case'in namespace'ine referans bulunmadı. Kanıt bagimsizlik-kontrolu.json içindedir.

Aynı repoda ve aynı solution içinde teslim etmek dokümanın şartıdır. Compose içindeki aynı SQL Server hizmeti yerel kurulum kolaylığı sağlar; iki ayrı veritabanının arasında ortak tablo veya cross-database sorgu bulunmaz. Farklı bağlantı adresleriyle iki ayrı SQL sunucusu da kullanılabilir.

## Tamamlanan kontroller

- Sıfır hata ve uyarıyla çözüm derlemesi.
- Docker/gerçek SQL Server üzerinde güncel toplam 43/43 test; başarısız veya atlanan test yok.
- İki ayrı API image'ının build edilmesi ve Production ortamında açılması.
- İki dış porttan HTTP 200 / Healthy; her sağlık kontrolü kendi veritabanına bağlanıyor.
- Canlı demo: sipariş 201, kısmi iptal 200, bayi teklifi 460 net / 552 KDV dahil, limit aşımında onaysız 409 ve açık onayla 201.
- Son kullanım kontrolü: aynı siparişi tekrar gönderince mevcut kimlik dönüyor ve stok değişmiyor; negatif miktar/fiyat, olmayan ürün ve hatalı JSON anlamlı hata döndürüyor. Sonuçlar son-kontrol.json içinde.
- Şema/başlangıç scriptleri, örnek HTTP istekleri, README, sorgu açıklamaları ve teknik kararlar mevcut.
- Bilinen NuGet açıkları kontrol sırasında giderildi; mevcut kaynaklarda bildirilmiş açık kalmadı. Bu sonuç bir sızma testi değildir.

SQL güvenilirlik ek kontrolü: veritabanı CHECK kısıtları, tekrar kurulumda stokların korunması, Case 2 kolon/tip/precision/scale/nullability/identity sözleşmesinin aynı kalması ve para değerlerinin API ile SQL tablolarında eşleşmesi gerçek SQL Server üzerinde doğrulandı. Kanıt sql-reliability-test-run.txt içindedir.

## Kaynak paketinin içeriği

Kaynak kod, SQL şema/başlangıç scriptleri, testler, Compose/Dockerfile'lar, açıklamalar ve doğrulama çıktıları bulunur. Derleme klasörleri bin/obj, .git geçmişi, .env ve yerel kullanıcı dosyaları pakete alınmaz. .env.example yalnız belgelenmiş yerel demo parolasını içerir.

SQL Server'ın çalışan veri dosyaları veya Docker volume'u yüklenmez. Yeni bir bilgisayarda Compose açılışındaki scriptler iki veritabanını, tabloları ve başlangıç kayıtlarını oluşturur. Bu makinedeki demo siparişi ve bakiye değişiklikleri yeni kurulumun başlangıç verisi değildir.

## Teslimi tamamlamak için gerekenler

1. İşverenin istediği düzenlenmemiş AI konuşma dökümü eklenmeli. AI_KONUSMA_DOKUMU.md şu anda yalnız bunun eksik olduğunu açıklar.
2. Gerçek çalışma süresi kaydedilmeli; 3 saat sınırına uyulduğu varsayılmamalı veya sonradan uydurulmamalı.
3. Mailin gönderildiği tarih/son teslim tarihi doğrulanmalı.
4. GitHub/GitLab hedef hesabı ve repo görünürlüğü seçilmeli; bağlantı ve yazma yetkisi doğrulanınca yüklenmeli. Bu inceleme sırasında dışarıya yayın yapılmadı.

KARARLAR.md kısa karar kaydıdır; ayrıntılı teknik açıklamalar docs/SORGULAR.md içinde tutulur. Kaynak ZIP'i inceleme paketidir; eksik AI dökümü nedeniyle eksiksiz nihai teslim olarak sunulmamalıdır.
