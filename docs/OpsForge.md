
Email: demo@opsforge.local
Password: Demo123!


alice@opsforge.local / Demo123!
Confirm you only see team-related services/infrastructure/deployments.

# OpsForge – İş Gereksinimleri ve Ürün Tanımı

# 1. Genel Bakış

OpsForge, kurumların uygulama, altyapı, dağıtım, operasyon ve güvenilirlik süreçlerini merkezi olarak yönetebilmeleri amacıyla geliştirilmiş bir İç Geliştirici Platformudur (Internal Developer Platform - IDP).

Modern kurumlarda uygulamalara ilişkin bilgiler birçok farklı sistemde dağınık halde bulunmaktadır:

* GitHub
* Azure DevOps
* Wiki Sistemleri
* Excel Dosyaları
* E-posta Grupları
* Monitoring Araçları
* Ticket Sistemleri

Bu durum uygulamaların operasyonel yönetimini zorlaştırmakta ve kurumsal görünürlüğü azaltmaktadır.

OpsForge'un amacı bu bilgileri merkezi bir platform altında toplayarak uygulamaların yaşam döngüsünü uçtan uca yönetilebilir hale getirmektir.

---

# 2. İş Problemi

Kurumlarda yüzlerce uygulama ve servis bulunmaktadır.

Bu uygulamalarla ilgili kritik soruların cevapları çoğu zaman hızlı şekilde bulunamamaktadır:

* Bu uygulamanın sahibi kim?
* Hangi ekip destek veriyor?
* Kaynak kodu nerede bulunuyor?
* Son ne zaman canlıya çıkıldı?
* Hangi ortamlarda çalışıyor?
* Hangi veritabanlarını kullanıyor?
* Hangi altyapıya bağlı?
* Şu anda sağlıklı çalışıyor mu?
* Son yaşanan operasyonel problemler nelerdi?
* Operasyonel dokümantasyonu nerede bulunuyor?

Bu soruların cevaplarının farklı sistemlerde tutulması operasyonel risk yaratmaktadır.

---

# 3. Ürün Vizyonu

OpsForge'un amacı;

* Uygulamaları
* Takımları
* Altyapıları
* Dağıtımları
* Olayları
* Sağlık durumlarını
* Dokümantasyonu

tek bir platform altında birleştirmektir.

Platform sadece manuel veri girişi yapılan bir sistem olmayacak, aynı zamanda GitHub ve diğer kaynak sistemlerle entegre olarak operasyonel verileri otomatik olarak toplayacaktır.

---

# 4. Hedef Kullanıcılar

## Platform Yöneticisi

Sistem genelinde yönetim ve yapılandırma işlemlerini yürütür.

---

## Takım Lideri

Takımına ait uygulamaların operasyonel durumunu takip eder.

---

## Yazılım Mühendisi

Günlük operasyonel süreçleri ve dağıtımları yönetir.

---

## DevOps / Platform Mühendisi

Altyapı ve dağıtım süreçlerini yönetir.

---

## Mühendislik Yöneticisi

Operasyonel metrikleri ve ekip performansını izler.

---

# 5. İş Hedefleri

## BH-01

Tüm uygulamalar için merkezi görünürlük sağlamak.

---

## BH-02

Uygulama sahipliklerini görünür hale getirmek.

---

## BH-03

Dağıtım süreçlerinin izlenebilirliğini artırmak.

---

## BH-04

Operasyonel bilgi kaybını azaltmak.

---

## BH-05

Incident çözüm sürelerini azaltmak.

---

## BH-06

Altyapı bağımlılıklarını görünür hale getirmek.

---

## BH-07

Operasyonel süreçleri denetlenebilir hale getirmek.

---

## BH-08

Mühendislik ekiplerine tek noktadan operasyon paneli sunmak.

---

# 6. Temel Modüller

# 6.1 Kimlik ve Yetkilendirme

Kullanıcıların sisteme güvenli erişimini sağlar.

Özellikler:

* Kullanıcı Yönetimi
* JWT Authentication
* Refresh Token
* Rol Yönetimi
* Takım Üyelikleri

Roller:

* Admin
* Team Lead
* Engineer

---

# 6.2 Takım Yönetimi

Kurum içindeki ekipleri ve ekip üyelerini yönetir.

Özellikler:

* Takım Oluşturma
* Takım Güncelleme
* Üye Yönetimi
* Takım Sahipliği

---

# 6.3 Servis Kataloğu

Kurumdaki tüm uygulama ve servislerin merkezi envanteridir.

Özellikler:

* Servis Oluşturma
* Servis Güncelleme
* Kritiklik Seviyesi
* Takım Ataması
* Repository Bağlantısı

Örnek Bilgiler:

* Uygulama Adı
* Açıklama
* Repository URL
* Kritiklik
* Sorumlu Takım

---

# 6.4 Ortam Yönetimi

Servislerin farklı çalışma ortamlarını yönetir.

Desteklenen Ortamlar:

* Development
* Test
* UAT
* Production

Her ortam için:

* URL
* Açıklama
* Durum Bilgisi

saklanabilir.

---

# 6.5 Altyapı Envanteri

Servislerin kullandığı altyapı bileşenlerini takip eder.

Desteklenen Varlık Tipleri:

* SQL Database
* PostgreSQL
* Redis
* App Service
* Virtual Machine
* Storage Account
* Key Vault
* Kubernetes Cluster

Her altyapı bileşeni ilgili servislerle ilişkilendirilebilir.

Örnek:

Frisk API

* Frisk SQL
* Frisk Redis
* Frisk Key Vault
* Frisk App Service

---

# 6.6 GitHub Entegrasyonu

OpsForge'un temel farklılaştırıcı özelliklerinden biridir.

Platform GitHub ile entegre çalışarak uygulamalara ilişkin operasyonel verileri otomatik olarak toplayabilir.

Toplanabilecek Bilgiler:

* Repository Bilgileri
* Açıklama
* Default Branch
* Commit Geçmişi
* Release Bilgileri
* GitHub Actions Çalışmaları
* Deployment Geçmişi
* Repository Sahipleri
* CODEOWNERS Bilgileri
* Takım Bilgileri

Bu sayede uygulama envanteri sürekli güncel tutulabilir.

---

# 6.7 Dağıtım Yönetimi

Uygulamaların yayın geçmişini takip eder.

Özellikler:

* Deployment Kaydı
* Sürüm Bilgisi
* Commit Bilgisi
* Release Notları
* Yayınlayan Kullanıcı
* Ortam Bilgisi

GitHub entegrasyonu sayesinde deployment kayıtları otomatik oluşturulabilir.

---

# 6.8 Sağlık İzleme Sistemi

Servislerin erişilebilirliğini ve çalışma durumunu sürekli kontrol eder.

Her servis için:

* Health Endpoint
* Ping Endpoint
* Status Endpoint

tanımlanabilir.

Örnek:

https://frisk.company.com/health

OpsForge belirli aralıklarla bu adresleri kontrol eder.

Takip Edilen Veriler:

* HTTP Durumu
* Yanıt Süresi
* Son Başarılı Kontrol
* Son Başarısız Kontrol
* Uptime Yüzdesi

---

# 6.9 Olay Yönetimi (Incident Management)

Operasyonel problemlerin yönetilmesini sağlar.

Özellikler:

* Incident Oluşturma
* Severity Seviyeleri
* Durum Takibi
* Yorum Sistemi
* Kök Neden Analizi
* Çözüm Bilgileri

Durumlar:

* Open
* Investigating
* Mitigated
* Resolved

---

# 6.10 Bildirim Yönetimi

Kritik olaylar için otomatik bildirim üretir.

Bildirim Kanalları:

* E-posta
* Uygulama İçi Bildirim
* Webhook

Örnek Olaylar:

* Servis Çöktü
* Deployment Başarısız
* Incident Açıldı
* Incident Çözüldü

---

# 6.11 Operasyonel Dokümantasyon

Servis bazlı operasyonel bilgi saklanmasını sağlar.

Örnek Dokümanlar:

* Runbook
* Deployment Rehberi
* Recovery Adımları
* Bilinen Problemler
* Destek Bilgileri

Markdown desteği sunulur.

---

# 6.12 Denetim Kayıtları (Audit Log)

Tüm kritik işlemler kayıt altına alınır.

Örnek Kayıtlar:

* Kullanıcı Girişi
* Servis Güncellemesi
* Deployment Oluşturulması
* Yetki Değişiklikleri
* Incident Güncellemeleri

Her kayıt aşağıdaki bilgileri içerir:

* Kullanıcı
* Tarih
* İşlem Tipi
* Varlık Tipi
* Varlık Kimliği

---

# 7. Beklenen Faydalar

OpsForge kullanımı sonucunda:

* Uygulama sahiplikleri merkezi olarak takip edilir.
* Operasyonel bilgi kaybı azaltılır.
* Dağıtım süreçleri görünür hale gelir.
* Altyapı bağımlılıkları takip edilir.
* GitHub ile entegrasyon sayesinde veri giriş yükü azalır.
* Servis sağlık durumları sürekli izlenir.
* Incident süreçleri standartlaştırılır.
* Operasyonel olgunluk artırılır.

---

# 8. Başarı Kriterleri

OpsForge kullanılarak aşağıdaki sorular birkaç dakika içerisinde cevaplanabilmelidir:

* Bu uygulamanın sahibi kim?
* Kaynak kodu nerede?
* Son deployment ne zaman yapıldı?
* Hangi commit canlıda?
* Hangi ortamlar mevcut?
* Hangi altyapı bileşenlerini kullanıyor?
* Servis şu anda çalışıyor mu?
* Son incident neydi?
* Operasyonel dokümanları nerede?
* Son değişikliği kim yaptı?

Bu soruların tamamının tek platform üzerinden cevaplanabiliyor olması OpsForge'un temel başarı kriteridir.
