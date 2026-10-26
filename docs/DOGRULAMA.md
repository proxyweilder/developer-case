# Doğrulama durumu

26 Ekim 2026 geliştirme ve kullanıcının paylaştığı Docker çalıştırma çıktısı:

| Kontrol | Sonuç |
|---|---|
| Yerel çözüm derlemesi | Başarılı, 0 uyarı / 0 hata |
| Case 1 | 21/21 başarılı: 11 birim + 10 gerçek SQL testi |
| Case 2 | 22/22 başarılı: 16 birim + 6 gerçek SQL testi |
| Toplam | 43 başarılı; başarısız veya atlanan test yok |
| SQL Server container | Başlatıldı ve Healthy durumuna geçti |
| Test container image | Başarıyla oluşturuldu |
| Case 1 / Case 2 runtime image ve açılış | İkisi de başarılı; startup-run.txt |
| http://localhost:5081/health | Doğrudan kontrol edildi: HTTP 200 / Healthy |
| http://localhost:5082/health | Doğrudan kontrol edildi: HTTP 200 / Healthy |
| Compose yapılandırması | config --quiet başarılı |
| Bağımlılık taraması | Kontrol edilen NuGet kaynaklarında bilinen açık bildirilmedi |

## Son SQL güvenilirlik doğrulaması

Dört SQL testi daha eklendi. Güncel sonuç Case 1 21/21, Case 2 22/22; toplam 43 başarılı, 0 başarısız, 0 atlanan testtir. Son çıktı sql-reliability-test-run.txt dosyasında saklandı.

- Doğrudan SQL ile negatif veya fiziksel stoğu aşan rezervasyon yazımı 547 constraint hatasıyla reddedildi; önceki stok korundu.
- Başlangıç scripti iki kez çalıştırılınca mevcut rezervasyon sıfırlanmadı ve üç demo ürün çoğaltılmadı.
- Cariler ve Teklifler kolonları dokümandaki tip, uzunluk, precision, scale, nullability ve identity özellikleriyle eşleşti; tekrar başlangıçta şema ve eski kayıtlar korundu.
- Satır başına 0.025 tutarlı iki satır, net 0.04 ve KDV dahil 0.05 verdi; API yanıtı, Teklifler kaydı ve Cariler bakiyesi eşleşti.

Testler yalnız rastgele isimli test veritabanlarını kullandı; demo kayıtlarına dokunulmadı.

## Önceki 39 testlik yeniden doğrulama

Gereksinim incelemesinde iki test eklendi ve iptal rollback doğrulaması güçlendirildi. Yerel .NET test çalıştırıcısı açık IPv4 adresi tcp:127.0.0.1,14333 üzerinden Docker SQL Server'a bağlandı. Case 1 19/19 ve Case 2 20/20 test geçti. Güncel kanıt review-test-run.txt içindedir. localhost ile yapılan önceki yerel deneme bağlantı zaman aşımı verdi; README örneği düzeltildi. Runtime iş kodu değiştirilmedi.

## Önceki Docker kanıtı ve kapsam

Docker komutunu kullanıcı kendi PowerShell terminalinde çalıştırdı; bu oturumun Docker motoruna erişimi kısıtlıdır. Sonuçlar paylaşılan tam çıktının incelenmesine dayanır. Düzenlenmemiş terminal çıktısı test-run.txt dosyasında saklandı.

```powershell
docker compose --profile test run --build --rm tests
```

Test edilen kritik SQL senaryoları: son bir ürüne eşzamanlı iki siparişte yalnız bir başarı; aynı anahtarın paralel tekrarında tek sipariş; aynı rezervasyonun çift iptalinin engellenmesi; çok satırlı iptalde geri alma; yetersiz/kısmi stok; aynı carinin paralel kredi limiti kontrolü ve açık limit onayı.

Çıktıdaki ExceptionHandlerMiddleware kaynaklı fail logları testlerin bilerek tetiklediği RuleViolation durumlarıdır. Testler bu durumların 404/409 gibi beklenen HTTP sonuçlarına çevrildiğini doğruladı. Bu log satırları testlerin başarısız olduğu anlamına gelmez. Mevcut .NET 8 hata middleware'i beklenen iş kuralı istisnalarını da Error seviyesinde kaydetmektedir.

Bu testler doğruluk testleridir; kapasite/yük ölçümü değildir.

## Normal çalıştırma doğrulaması

Kullanıcının docker compose up --build çıktısında iki API runtime image'ı başarıyla oluşturuldu; OrdersCase ve QuotesCase açıldı; her API Production ortamında 8080 iç portunda dinlemeye başladı. Ham çıktı startup-run.txt dosyasında saklandı.

Ayrıca bu oturumdan iki dış HTTP adresine doğrudan GET isteği yapıldı:

- http://localhost:5081/health → HTTP 200, Healthy.
- http://localhost:5082/health → HTTP 200, Healthy.

Sağlık kontrolü her API'nin kendi SQL bağlantısında SELECT 1 çalıştırdığı için veritabanı erişimi de doğrulandı. Bu adımda demo verilerinde değişiklik yapılmadı.

## Teslim öncesi kalanlar

AI konuşma dökümü, gerçek çalışma süresi ve kesin son teslim tarihi tamamlanmalıdır. GitHub/GitLab bağlantısı veya kaynak kod ZIP'i teslim biçimi olarak seçilmelidir. Çalışan API'lerin kullanımı README.md ve requests klasöründeki örneklerde açıklanmıştır.
