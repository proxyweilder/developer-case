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
- Docker/gerçek SQL Server üzerinde güncel toplam 54/54 test (31 Ekim; 28 Case 1, 26 Case 2); başarısız veya atlanan test yok.
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

## Son teslim kontrolü

31 Ekim yeniden kontrolü SON_TESLIM_KONTROLU.md içinde kayıtlıdır. Ek 11 SQL senaryosu geçti; yerel ve Docker testleri aynı 54 testi başarıyla çalıştırdı. KARARLAR.md metninin tamamını içeren KARARLAR.pdf bir sayfa olarak render edildi ve görsel kontrolü yapıldı. Ham konuşma kopyası 267 mesaja güncellendi. Kaynak ZIP'i güncel dosyalardan yeniden oluşturuldu.

## Teslimi tamamlamak için gerekenler

1. Kesin aktif çalışma süresi ayrı sayaçla ölçülmedi; süreye ilişkin gerçeğe aykırı bir tamamlanma iddiası eklenmemeli.
2. Private GitHub reposuna değerlendirici erişimi verilmeli veya güncel kaynak ZIP'i teslim edilmeli. Repo: https://github.com/proxyweilder/developer-case. Kullanıcı 31 Ekim 2026 tarihinde güncel kaynakların commit ve push işlemini onayladı.

KARARLAR.md kısa karar kaydıdır; ayrıntılı teknik açıklamalar docs/SORGULAR.md içinde tutulur.
