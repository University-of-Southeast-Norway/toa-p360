# Oversikt
Løsningen **Løpende overføring av avtaler fra TOA til P360**, heretter referert til som **løsningen**, overfører, som navnet indikerer, avtaler som signeres fortløpende fra TOA til P360. For alle avtaler som signeres i ToA vil det legges ut en melding på DFØ's meldingskø system. Når en melding legges på køen trigges det en funksjon i løsningen som henter avtale informasjon fra ToA og overfører til P360. Løsningen vil forsøke å gjenbruke eksisterende person og sak i P360 dersom løsningen får match på dette i P360. Det er også lagt inn en sikring som gjør at samme avtale ikke vil lastes opp på nytt dersom den tidligere er lastet opp via denne løsningen.
Følgende data hentes fra TOA og benyttes i løsningen:
-	Avtalenummer
-	Avtale-fil (PDF)
-	Fødselsnummer
-	Navn
-	Adresse
-	Mobilnummer
-	E-port adresse

Persondata blir brukt dersom løsningen ser at det er behov for å opprette ny person i P360. Fødselsnummer brukes til å gjenfinne personen i P360.
Avtalenummer benyttes til å definere filnavnet til selve avtale-filen (PDF) i P360. Avtale-filen lastes opp som den er på et nytt dokument.
Metadata på sak og dokument i P360 defineres i egne definisjonsfiler som kommer med løsningen. Disse settes opp før løsningen startes. Disse definisjonsfilene kommer med et standard oppsett, men det vil også være behov for et tilpasset oppsett for hver institusjon.

# Hvordan komme i gang
1. Opprett en avtale om bruk av DFØ API'er via institusjonens kundekontakt mot DFØ (mer info på [DFØ API-portal](https://api-portal.dfo.no))
2. Bestille API-nøkkel til Public 360 API (SIKT/Tieto) med lese/skrivetilgang, samt opprette integrasjonsbruker dersom dette ikke allerede eksisterer
3. Bestill virksomhetssertifikat fra [Buypass](https://www.buypass.no) eller [Commfides](https://www.commfides.no), eventuelt gjenbruke eksisterende
4. Opprett en integrasjon mot Maskinporten i [Samarbeidsportalen til Digdir](https://samarbeid.digdir.no)
   * Integrasjonen skal settes opp med scope ```dfo:ansatte dfo:ansatte/infokontrakter dfo:infokontrakter/filer```
5. Last ned [binærfiler]() eller [kildekode]()
6. Installer som [Windows Service](#installasjon-og-eksekvering-av-windows-service) eller Linux Docker
7. Sett opp [definisjonsfiler](#definisjonsfiler)
8. Sett opp [miljøkonfigurasjon](#oppsett-av-miljøkonfigurasjon)
9. Start tjenesten

# Definisjonsfiler
## Teknisk
Disse filene finner man i mappen _Definitions_ under installasjonsmappen til løsningen. Dette er JSON filer, og hver fil kommer i to versjoner: Filen som blir brukt ved kjøring, og en _template_ fil som inneholder fullstendig oppsett. Filen som blir brukt ved kjøring inneholder også et forslag til oppsett slik at man har et utgangspunkt.

## Oppsett
Selv om filene potensielt kan inneholde mange definisjoner er det ikke nødvendig å sette opp alle. Men det er noen grunnleggende felter som et er verdt å merke seg. Enkelte felter er nødvendig, samt at enkelte felter også medfører noe logikk i selve løsningen dersom spesifikke verdier angis.
Felles for alle filene er at de settes opp med feltet _ADContextUser_, som angir brukeren som definerer at endringene i P360 er gjort av en maskin og ikke en reell bruker.

### Finne eksisterende person
_get_private_persons.json_

Søker etter eksisterende personer basert på metadata gitt i definisjonsfilen og fødselsnummer fra TOA. Metadata som settes i denne filen må matche metadata som er definert i _synchronize_private_person.json_ for at gjenbruk skal fungere optimalt.

### Opprette ny person
_synchronize_private_person.json_

### Finne eksisterende sak
_get_cases.json_

Søker etter saker basert på metadata gitt i definisjonsfilen og recno fra eksisterende person. Metadata som settes opp i denne filen må matche metadata som er definert i _create_case.json_ for at gjenbruk skal fungere optimalt.

### Opprette ny sak
_create_case.json_

Dersom feltet Contacts er satt opp settes feltet _ReferenceNumber_ automatisk lik _'recno:\<recno\>'_ der recno er recno til eksisterende eller opprettet privat person.
Følgende felter må settes:
- Title
- Status
- AccessCode
- AccessGroup

### Sjekke om avtalen er lastet opp tidligere
_get_documents.json_
  
Hver gang løsningen laster opp en avtale til P360 settes det en unik verdi i _Notat_ feltet tilknyttet avtale-filen i P360. Dette feltet benyttes til å kontrollere om filen er lastet opp tidligere. Dersom man ønsker ytterligere begrensninger kan dette settes i definisjonsfilen. Det er ellers ingen spesifikke behov i denne filen utover _ADContextUser_.

### Opprette nytt dokument
_create_document.json_

Dersom feltet Contacts er satt opp med Role lik «Avsender» settes _ReferenceNumber_ automatisk lik 'recno:\<recno\>', der _\<recno\>_ er recno til eksisterende eller opprettet privat person.
Følgende felter må settes:
- Title
- Status
- AccessCode
- AccessGroup

### Laste opp filen til dokument
_update_document.json_

Det er ingen spesifikke behov i denne filen utover _ADContextUser_.

### Avslutte dokument
_sign_off_document.json_

Følgende felter må settes:
- ResponseCode

# Teknisk oppsett
## Eksekvering av løsningen
Løsningen kan kjøres på både Windows og Linux. Den kan installeres som en Windows Service på Windows, og den kan kjøres i en Docker container på Linux. Løsningen kan også eksekveres fra  et kommando-vindu.

### Installasjon og eksekvering av Windows Service
Last ned siste versjon av pakken 'ToaArchiver.WindowsService'. Kjør følgende kommandoer i PowerShell:

``` PS
# Installasjon
sc.exe create "ToA Archiver" binpath="c:\path\to\ToaArchiver.WindowsService.exe"

# Start
sc.exe start "ToA Archiver"

# Stopp
sc.exe stop "ToA Archiver"

# Slette
sc.exe delete "ToA Archiver"

# Sjekke logg
Get-Content log{dato:yyyymmdd}.txt -wait

```

## Oppsett av miljøkonfigurasjon
Konfigurasjon av miljø variable settes opp i en fil kaldt _appsettings.json_. Det følger med en _appsettings.template.json_ som inneholder alle parameterne som skal settes opp. En del parametere er forhåndsdefinert, og det er ikke behov for å endre disse.

### Parametere:
```
P360:Intark:
// Seksjonen Sif settes opp ved direkte tilkobling mot P360 API. Seksjonen Intark settes dersom tilkobling til P360 går via IntArk.

// Endepunkt til P3560 API i IntArk
P360:Intark:service_name:BaseAddress

// Passord til P3560 API i IntArk
P360:Intark:service_name:ApiKey

// Service bruker som benyttes ved kobling mot P360 API.
P360:Sif:AdContextUser

// https://{institusjon}.public360online.com.
P360:Sif:BaseAddress

// API nøkkel/passord som brukes til å koble mot P360.
P360:Sif:ApiKey

// Settes til første dato denne løsningen ble tatt i bruk. Dersom løsningen for batch-vis overføring er benyttet for å hente historiske data, settes denne til samme dato som ble benyttet ved batch-vis overføring.
AppendCaseOptions:InProductionDate

// PROD: https://api.dfo.no/ TEST: https://api-test.dfo.no/
// Dersom IntArk benyttes settes denne til base-address for DFØ API'et i IntArk.
Dfo:Api:BaseAddress

// True: Alle meldinger som er håndtert av denne løsningen kvitteres ut i meldingskøen som utført.
Dfo:Queue:AckHandledMessages

// True: Alle meldinger kvitteres ut i meldingskøen som utført. Overstyrer valget i 'AckHandledMessages'.
Dfo:Queue:AckAllMessages

// Meldingskø satt opp av DFØ.
Dfo:Queue:Queue

// Port oppgitt av DFØ. Som regel 5671.
Dfo:Queue:Port

// Satt opp av DFØ.
Dfo:Queue:VirtualHost

// PROD: mq.dfo.no TEST: mq-test.dfo.no
Dfo:Queue:Host

// Satt opp av DFØ.
Dfo:Queue:User

// Satt opp av DFØ.
Dfo:Queue:Password

// Virksomhetssertifikatet som benyttes for å få token fra Maskinporten.
Dfo:Maskinporten:Certificate:Path

// Passord til virksomhetssertifikatet.
Dfo:Maskinporten:Certificate:Password

// PROD: https://maskinporten.no/ TEST: https://ver2.maskinporten.no/
Dfo:Maskinporten:Audience

// PROD: https://maskinporten.no/token TEST: https://ver2.maskinporten.no/token
Dfo:Maskinporten:TokenEndpoint

// Issuer hentes fra Samarbeidsportalen (Integrasjons-ID).
Dfo:Maskinporten:Issuer

// Oppsett for autentisering dersom IntArk benyttes. Settes for hvert scope.
Dfo:ApiKeys:value

```
