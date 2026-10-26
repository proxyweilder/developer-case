# Yazılım Geliştirici Case Çalışması

Tek repo içinde iki bağımsız .NET 8 Web API vardır. Case 1 sipariş ve stok rezervasyonunu, Case 2 mevcut VB.NET teklif hesaplamasını uygular. Her case'in kendi API, iş kuralları, test projesi ve veritabanı bulunur.

## İlk çalıştırma

Gerekenler: Linux container motoru çalışan Docker Desktop ve Docker Compose. Container üzerinden çalıştırmak için ayrıca .NET veya SQL Server kurulumu gerekmez. İlk çalıştırma image ve paket indirdiği için birkaç dakika sürebilir.

Proje klasöründe PowerShell veya terminal açın:

```powershell
docker compose up --build
```

- Case 1: http://localhost:5081
- Case 2: http://localhost:5082
- Sağlık kontrolü: her API için `/health`. `Healthy` yanıtı SQL bağlantısının da çalıştığını gösterir.
- SQL Server: `127.0.0.1,14333`. Veritabanları `OrdersCase` ve `QuotesCase`.

Logları ayrı terminalde görmek için `docker compose logs -f case1 case2` kullanın. Durdurmak için `docker compose down` çalıştırın. Veriler Docker volume'unda saklanır; normal durdurma ve yeniden başlatma stokları sıfırlamaz.

Compose içindeki parola yerel demo içindir. Değiştirmek için `.env.example` dosyasını `.env` olarak kopyalayıp `SQL_PASSWORD` değerini düzenleyin. `.env` Git'e eklenmez. Mevcut SQL volume'u varsa yalnız environment değerini değiştirmek sunucudaki parolayı değiştirmez.

## Yalnız bir case'i çalıştırma

```powershell
docker compose up --build case1
# veya
docker compose up --build case2
```

Her komut gereken SQL hizmetini de başlatır. İki API birbirini çağırmaz. Paylaşılan SQL Server container'ı yalnız yerel kurulum kolaylığı içindir; bağlantı adresleri değiştirilerek ayrı SQL sunucularında da çalıştırılabilirler.

## İlk siparişini oluştur

PowerShell'de:

```powershell
# 1. Stok durumunu kontrol et.
Invoke-RestMethod 'http://localhost:5081/urunler/URN-001/stok'

# 2. Sipariş oluştur. Her yeni siparişte yeni anahtar kullan.
$body = @{
    stokTercihi = 'tumunuReddet'
    satirlar = @(@{ urunKod = 'URN-001'; miktar = 3 })
} | ConvertTo-Json -Depth 4

$order = Invoke-RestMethod -Method Post `
    -Uri 'http://localhost:5081/siparisler' `
    -Headers @{ 'Idempotency-Key' = 'ilk-siparis-001' } `
    -ContentType 'application/json' -Body $body

# 3. Kaydedilen siparişi görüntüle.
$order | ConvertTo-Json -Depth 5

# 4. Siparişin kalan rezervasyonunun tamamını iptal et.
Invoke-RestMethod -Method Post `
    -Uri "http://localhost:5081/siparisler/$($order.id)/iptal" `
    -ContentType 'application/json' -Body '{"satirlar":null}'
```

Aynı anahtar ve aynı içerik tekrar gönderilirse yeni sipariş oluşmaz. Yanıt aynı sipariş kimliğini ve siparişin güncel durumunu döndürür. Aynı anahtarla farklı içerik göndermek `409` hatası verir. Anahtar büyük/küçük harfe duyarlıdır. Başarısız ve tamamen geri alınmış bir deneme anahtarı tüketmez.

Başlangıç stokları: `URN-001: 10`, `URN-002: 1`, `URN-003: 0`. Önceden yapılan denemeler bu stokları değiştirmiş olabilir.

## Case 1 istekleri

| Metot | Adres | İşlev |
|---|---|---|
| POST | `/siparisler` | Sipariş oluşturur ve stok rezerve eder |
| GET | `/siparisler/{id}` | Sipariş ve bütün satırlarını getirir |
| POST | `/siparisler/{id}/iptal` | Tam veya satır bazında kısmi iptal yapar |
| GET | `/urunler/{kod}/stok` | Fiziksel, rezerve ve kullanılabilir stoğu getirir |

`stokTercihi` zorunludur:

- `tumunuReddet`: Bir satır bile karşılanamıyorsa hiçbir stok değişmez, sipariş oluşmaz; `409` döner.
- `uygunMiktariAl`: Her satırın mevcut miktarı rezerve edilir. Örneğin istenen 5, mevcut 2 ise rezerve 2 olur. Sıfır stoklu satır yanıtta rezerve 0 olarak görünür. Bütün satırlar sıfır kalıyorsa sipariş oluşturulmaz.

İstek başına en fazla 100 satır kabul edilir. Bir ürün tek satırda bulunmalıdır. Miktar pozitif tam sayı olmalıdır. Ürün kodları boşlukları temizlenip büyük harfe çevrilir; sıralama farklı olsa bile aynı sipariş içeriği aynı kabul edilir.

Sipariş yanıtındaki `istenen` müşterinin istediğini, `rezerve` ilk rezervasyonu, `iptal` geri bırakılan miktarı, `aktifRezervasyon` hâlen ayrılmış miktarı gösterir. Durumlar: `rezerveEdildi`, `kismenRezerveEdildi`, `kismenIptalEdildi`, `iptalEdildi`.

Kısmi iptal gövdesi:

```json
{"satirlar":[{"urunKod":"URN-001","miktar":1}]}
```

Tam iptal gövdesi: `{"satirlar":null}`. Tam iptal yalnız kalan rezervasyonu bırakır. Tamamen iptal edilmiş satır tekrar iptal edilemez. Bir iptal isteğinde bir satır geçersizse bütün iptal geri alınır. İptal fiziksel stoğu artırmaz; rezerve miktarı azaltır.

## Case 2 teklif oluşturma

`POST http://localhost:5082/teklifler`

```json
{
  "cariKod":"CARI-001",
  "satirlar":[{"miktar":50,"birimFiyat":10}],
  "krediLimitiAsiminiOnayla":false
}
```

Demo `CARI-001` bayi, limit 100000; `CARI-002` perakende, limit 100'dür. Başlangıç bakiyeleri sıfırdır; tekrar çalıştırma mevcut bakiyeyi sıfırlamaz. Demo cariler yalnız `SeedDemoData=true` iken eklenir.

Örnekte bayi iskontosu toplam %8, net tutar 460 ve KDV dahil tutar 552 olur. Başarılı yanıt `201` ile teklif kimliğini, tutarları ve yeni bakiyeyi döndürür.

Limit aşımında `409` ve `kredi_limiti_onayi_gerekli` döner; henüz hiçbir kayıt yazılmaz. Kullanıcı onay verirse aynı bilgileri `krediLimitiAsiminiOnayla:true` ile yeniden gönderin. API güncel bakiyeyi tekrar okuyarak kontrol eder. Onay, güncel tutarın limit üstünde kaydedilmesine açık izin anlamına gelir; fiyatı veya bakiyeyi dondurmaz.

Eski koddaki 100 adet iskontosu bilerek korunmuştur: 100 ve üzeri miktar da %5 alır; bayi için ayrıca %3 eklenir. Bunun nedenleri ve diğer kararlar `KARARLAR.md` içindedir. Case 2'de tekrar gönderim tekilliği kapsam dışıdır; başarılı teklifi yeniden göndermek yeni teklif oluşturur.

## Örnek istek dosyaları

`requests/case1.http` ve `requests/case2.http` sırayla denenebilecek örnekler içerir. VS Code REST Client eklentisi gibi bir HTTP istemcisiyle açılabilir; bu eklenti zorunlu değildir. PowerShell, curl veya Postman da kullanılabilir. Sayılar JSON formatında nokta ile yazılır: `10.50`.

## Hata mesajını okuma

İş kuralı hatalarında gövde `application/problem+json` olur:

```json
{
  "title":"Siparişin tamamı için stok yetersiz; hiçbir rezervasyon yapılmadı.",
  "status":409,
  "instance":"/siparisler",
  "kod":"yetersiz_stok",
  "istekKimligi":"sunucu-tarafindan-uretilir",
  "ayrinti":{"istenen":2,"kullanilabilir":1}
}
```

| Durum | Anlamı | Kullanıcı ne yapmalı? |
|---|---|---|
| 400 | Eksik/yanlış alan, geçersiz JSON veya miktar | Mesajdaki alanı düzelt |
| 404 | Ürün, sipariş veya cari bulunamadı | Kodu/kimliği kontrol et |
| 409 | Stok yetersiz, geçersiz iptal veya limit onayı gerekli | İlgili iş kuralını karşıla |
| 503 | Kilit beklemesi/zaman aşımı veya geçici yoğunluk | Siparişte aynı anahtarla tekrar dene |
| 500 | Beklenmeyen hata | İstek kimliğiyle logu incele |

Kilit beklemesi SQL sorgu zaman aşımıyla sınırlıdır. Deadlock olması halinde işlem geri alınır; veri güvenliğini koruyarak anlaşılır geçici hata döner. Ölçülmüş bir performans veya gecikme garantisi verilmez.

## Testleri çalıştır

Gerçek SQL Server dahil tüm testler için:

```powershell
docker compose --profile test run --build --rm tests
```

Testler `Case1Tests_<rastgele>` ve `Case2Tests_<rastgele>` isimli ayrı veritabanları açar ve sonunda yalnız bu test veritabanlarını siler. Demo siparişlerine veya bakiyelerine dokunmaz. SQL kullanıcısının test veritabanı oluşturma/silme yetkisi gerekir.

Kapsanan kritik durumlar: son bir ürüne iki eşzamanlı sipariş, aynı anahtarın paralel tekrarları, aynı rezervasyonun paralel iptali, çok satırlı işlemde geri alma, kısmi stok kabulü, iskonto sınırları ve yuvarlama, aynı carinin eşzamanlı kredi limiti kontrolü, açık limit aşımı onayı.

Docker kullanmadan yalnız hesaplama testleri için .NET 8 SDK ile:

```powershell
dotnet test DeveloperCase.sln
```

`SQL_TEST_CONNECTION` verilmezse gerçek veritabanı testleri **atlandı** olarak görünür. Bu sonuç paralel stok güvenliğinin doğrulandığı anlamına gelmez. Yerel SQL ile bütün testleri çalıştırmak için:

```powershell
$env:SQL_TEST_CONNECTION = 'Server=tcp:127.0.0.1,14333;Database=master;User Id=sa;Password=CaseDemo_2026!Sql;Encrypt=True;TrustServerCertificate=True'
dotnet test DeveloperCase.sln
```

## Kodu nereden okumalıyım?

1. İlgili case'in `Domain` projesindeki `Orders.cs` veya `Quotes.cs`: modeller ve saf iş kuralları.
2. `Api/Program.cs`: HTTP adresleri ve bağımlılıkların kaydı.
3. `Api/Infrastructure/*Store.cs`: transaction içindeki işlem sırası.
4. `Api/Infrastructure/*Queries.cs`: açıklamalı, parametreli SQL sorguları.
5. `Api/Database/schema.sql`: tablo şeması ve başlangıç verisi.
6. `tests/Case*.Tests`: iş kurallarının beklenen sonuçları ve gerçek SQL senaryoları.

Sorguların adım adım açıklaması, kilitlerin nedenleri ve performans sınırları `docs/SORGULAR.md` dosyasındadır.

## Kapsam ve teslim durumu

- UI, kullanıcı girişi, yetkilendirme ve üretim dağıtımı bu case'in kapsamı değildir. Compose yalnız yerel makineye port açar.
- Yerel demo, başlangıç şeması oluşturabilmek için `sa` kullanır. Gerçek ortamda şema kurulumu ayrı bir dağıtım adımı, uygulama hesabı sınırlı yetkili olmalıdır.
- Masaüstü uygulamasının orijinal kodu transaction kullanmadığından eski uygulama ile API birlikte çalışırken kredi kontrolünü uçtan uca garanti etmek için masaüstü yazma akışının da düzeltilmesi gerekir.
- İlk Docker çalıştırmasında 37/37 test geçti (docs/test-run.txt). Son incelemede rollback ve SQL güvenilirlik kontrolleriyle gerçek SQL Server üzerinde Case 1 için 21/21, Case 2 için 22/22; toplam 43/43 test geçti. Atlanan veya başarısız test yok. Güncel çıktı docs/sql-reliability-test-run.txt, önceki 39 testlik kontrol docs/review-test-run.txt ve gereksinim incelemesi docs/GEREKSINIM_INCELEMESI.md içindedir. Ayrı API container image’ları da başarıyla oluşturuldu; 5081 ve 5082 portlarındaki sağlık kontrolleri doğrudan HTTP 200 / Healthy döndürdü.
- İşverenin toplam en fazla 3 saat çalışma sınırı ve mailden itibaren bir hafta teslim süresi geçerlidir. Gerçek süre ve varsa eksik kalan işler teslim öncesi doğru biçimde kaydedilmelidir.
- AI konuşmalarının düzenlenmemiş dökümü ayrıca teslim edilmelidir. `AI_KONUSMA_DOKUMU.md` bu gereksinimin durumunu açıklar.
