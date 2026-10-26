# Sorguları ve paralel işlemleri anlama

Bu rehberin amacı sorguların ne yaptığını, hangi veriyi kilitlediğini ve neden bu sırada çalıştığını açıklamaktır. HTTP alan adları Türkçe, yeni stok tablolarının kolonları İngilizce; eski teklif tabloları dokümandaki adlarıyla korunmuştur.

## Bağımsızlık

Case 1 yalnız `OrdersCase`, Case 2 yalnız `QuotesCase` veritabanına bağlanır. Bir case'in Domain veya API projesi diğerine referans vermez. Ortak kullanılan paketler Dapper ve SqlClient'tır; ortak bir iş kuralı veya paylaşılan tablo yoktur. Küçük Database ve hata işleme sınıfları case'lerin tek başına alınabilmesi için her iki API içinde bulunur.

## Parametreli sorgu ne sağlar?

```sql
SELECT CariKod, Grup, KrediLimiti, Bakiye
FROM dbo.Cariler
WHERE CariKod = @code;
```

`@code` kullanıcının verisidir; SQL komutunun parçası değildir. Bir kullanıcı tırnak veya SQL komutu içeren bir değer gönderse de bu yalnız bir cari kodu olarak değerlendirilir. Para değerleri metne çevrilmeden decimal parametre olarak gönderildiği için Türkçe/İngilizce bilgisayarlardaki virgül farkı sorguyu bozmaz.

Dinamik kalan tek SQL tanımlayıcısı başlangıçta oluşturulan veritabanı adıdır. Kullanıcı isteğinden gelmez; yapılandırmadan okunur ve yalnız harf, rakam ve alt çizgi kabul edilir.

## Case 1 sipariş oluşturma sırası

1. Satırları doğrula; ürün kodlarını temizle ve sırala. Yanlış miktarı veritabanına gitmeden reddet.
2. Bir bağlantı aç, transaction başlat. Transaction tüm değişikliklerin beraber tamamlanmasını sağlar.
3. `sp_getapplock` ile yalnız bu `Idempotency-Key` değerinin kilidini al. Farklı anahtarlı istekler bu kilitte birbirini beklemez. Transaction bitince kilit otomatik bırakılır.
4. Anahtar daha önce kullanılmışsa gövde özetini karşılaştır. Aynı içerikte aynı siparişi döndür; farklı içerikte `409` ver.
5. Ürünleri kod sırasıyla `UPDLOCK, HOLDLOCK` kullanarak oku. Her ürünün mevcut rezervasyonunu transaction bitene kadar diğer yazarlardan koru.
6. Her satır için rezerve edilecek miktarı hesapla. Bütün satırlar uygunsa sipariş başlığını yaz.
7. Tüm rezervasyonları tek JSON parametresiyle SQL'e gönder. `OPENJSON` bunu geçici bir satır kümesine çevirir; stok güncelleme ve sipariş satırı ekleme tek komutla yapılır.
8. Sonuçları oku ve commit yap. Herhangi bir aşamada hata olursa transaction kapatılırken bütün değişiklikler geri alınır.

`UPDLOCK` güncelleme niyetini baştan bildirir. `HOLDLOCK` ilgili anahtar/aralık kilidini transaction sonuna kadar tutar. İkinci bir sipariş aynı ürünü isterse ilk işlem bittikten sonra güncel rezervasyonu okur. Fiziksel 1, rezerve 0 iken ilk işlem rezerveyi 1 yapar; ikinci işlem kullanılabilir miktarı 0 görür.

SQL Server gerektiğinde satırdan daha geniş kilit kullanabilir. Bu nedenle farklı ürünlerin hiçbir zaman beklemeyeceği veya deadlock'un imkânsız olduğu iddia edilmez. Tutarlı ürün sırası deadlock olasılığını azaltır; SQL zaman aşımı/deadlock durumunda işlem geri alınır ve API `503` döner.

Veritabanındaki `CHECK` kısıtı da `0 <= Reserved <= Physical` şartını korur. Yalnız uygulamadaki `if` kontrolüne güvenilmez. Fiziksel stok sipariş sırasında düşmez; rezervasyon artar.

## İptal neden transaction içinde?

İptal hem ürünün rezerve miktarını azaltır hem sipariş satırının iptal miktarını artırır. Yalnız birinin gerçekleşmesi veri tutarsızlığı oluşturur. Aynı siparişin iptal işlemleri sipariş başlığındaki güncelleme kilidiyle sıraya alınır. İkinci işlem ilk işlemden sonra kalan miktarı okur.

Tam iptal kalan rezervasyonu, kısmi iptal verilen miktarı bırakır. Satırlar ürün kodu sırasıyla güncellenir. En fazla 100 satır sınırı vardır. Sonraki satırda hata bulunursa önceki satırın değişikliği de geri alınır. Mevcut fiziksel stok artırılmaz.

## Okuma sorguları

- Ürün stoğu, ürün kodundaki birincil anahtarla tek kayıt okur. `kullanilabilir` değeri aynı kayıttaki fiziksel ve rezerve miktardan hesaplanır.
- Sipariş okuma tek veritabanı komutunda başlık ile bütün satırları döndürür. Her satır için ayrı okuma yapılmaz.
- `OrderLines` bileşik birincil anahtarı `(OrderId, ProductCode)`, bir siparişin satırlarını bulmayı ve kod sırasıyla getirmeyi destekler.
- `Orders.RequestKey` benzersiz indeksi tekrar kontrolünü hızlandırır ve aynı anahtarın ikinci kez kaydedilmesini veritabanında da engeller.
- `SELECT *`, `NOLOCK`, kilitli sorguları atlama veya uygulama içi global kilit kullanılmaz. `NOLOCK` tamamlanmamış veri gösterebilir ve stok doğruluğu için uygun değildir.

Sipariş oluştururken ürün kilitleri birer birer alınır; bunun amacı tutarlı kilit sırasıdır. Bu yol N ürün için N küçük kilitli okuma yapar. En fazla 100 satırlı case kapsamında bu açıklık tercih edilmiştir. Yazmalar topludur. Çok büyük siparişler için TVP/stored procedure ve gerçek yürütme planlarıyla ölçüm yapılabilir.

## Case 2 teklif sırası

1. Cari kodu, miktarı ve fiyatı doğrula.
2. Transaction aç. Cariyi `UPDLOCK, HOLDLOCK` ile oku.
3. SQL'den bağımsız `QuoteRules` sınıfında satır iskontolarını ve KDV'yi hesapla.
4. Güncel bakiye üzerinde kredi kontrolü yap. Onay yoksa `409` ver; henüz kayıt yazılmamıştır.
5. Teklif ekleme ve bakiye artırmayı aynı komut ve transaction içinde çalıştır.
6. Commit yap, teklif kimliği ve yeni bakiyeyi döndür.

Aynı cariye gelen iki API isteği sırayla güncel bakiye görür. Farklı cariler üzerinde uygulama düzeyinde ortak bir kilit yoktur. SqlClient bağlantı havuzu kullanır; her işlem kendi bağlantısını açar ve `await using` ile bırakır. SQL çağrıları asenkron ve iptal belirteciyle yapılır. Aynı connection/transaction üzerinde `Task.WhenAll` ile paralel komut çalıştırılmaz.

**Eski masaüstü uygulamasının sınırı:** API'nin aldığı güncelleme kilidi diğer SQL yazmalarını bekletebilir; fakat eski masaüstü kodu kredi kontrolünü transaction dışında yaptıysa daha sonra eskimiş bir karar üzerinden yazabilir. Şema korunarak yalnız API'yi düzeltmek bu eski karar akışını geriye dönük güvenli yapmaz. Masaüstü tarafının da transaction ve aynı kredi kontrolü disiplinine geçirilmesi işverene sorulmalıdır.

## Eski hesaplamayı okuma

- 50 ve üzerinde %5 iskonto uygulanır. Eski `ElseIf miktar >= 100` dalı hiçbir zaman çalışmaz; davranış korunmuştur.
- BAYI için iskonto oranına 3 puan eklenir: %5 + %3 = %8.
- Her satır önce iki basamağa `ToEven` ile yuvarlanır, sonra satır toplamları toplanır.
- KDV %20'dir. Kredi kontrolü eski koddaki gibi henüz iki basamağa indirilmemiş KDV'li tutarı kullanır.
- Veritabanına yazılan para iki basamaklıdır; SQL Server decimal dönüşümüyle uyumlu yuvarlama uygulanır.
- Teklif tarihi masaüstü davranışıyla uyumlu `GETDATE()`, Case 1 sipariş tarihi UTC'dir.

## Performans nasıl doğrulanmalı?

Paralel HTTP istekleri ile SQL Server'ın bir sorgu planını birden fazla işlemci üzerinde çalıştırması ayrı konulardır. Bu projede odak paralel isteklerin doğru sonuç vermesidir. Sorgulara gelişigüzel `MAXDOP` veya ek indeks eklenmez.

Gerçek ölçüm için aynı ürün ve farklı ürün senaryolarında başarı/çakışma oranı, p95 gecikme, bağlantı havuzu beklemesi, logical reads ve kilit beklemeleri izlenmelidir. Mevcut testler doğruluk testidir; bir yük testi veya üretim kapasitesi garantisi değildir.

Microsoft kaynakları: [Tablo kilit ipuçları](https://learn.microsoft.com/en-us/sql/t-sql/queries/hints-transact-sql-table), [sp_getapplock](https://learn.microsoft.com/en-us/sql/relational-databases/system-stored-procedures/sp-getapplock-transact-sql).
