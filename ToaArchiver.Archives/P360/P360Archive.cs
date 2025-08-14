using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using P360Client.Domain;
using P360Client.Domain.Factory;
using P360Client.Domain.Model;
using P360Client.DTO;
using ToaArchiver.Domain.Core;
using ToaArchiver.Domain.DTO;

namespace ToaArchiver.Archives.P360
{
    public class P360Archive : IArchive
    {
        private readonly ICaseFactory _caseFactory;
        private readonly P360ArchiveOptions _options;
        private readonly ILogger<P360Archive> _logger;

        public P360Archive(ICaseFactory caseFactory, IOptions<P360ArchiveOptions> options, ILoggerFactory loggerFactory)
        {
            _caseFactory = caseFactory;
            _options = options.Value;
            _logger = loggerFactory.CreateLogger<P360Archive>();
        }

        public void UploadSignedContract(SignedContractData uploadFileRequirements)
        {
            throw new NotImplementedException($"{nameof(P360Archive)} is based on async logic.");
        }

        public async Task UploadSignedContractAsync(SignedContractData uploadFileRequirements)
        {
            P360Client.Domain.Model.PrivatePerson caseOwner = new()
            {
                Nin = uploadFileRequirements.SocialSecurityNumber,
                FirstName = uploadFileRequirements.FirstName,
                MiddleName = uploadFileRequirements.MiddleName,
                Surname = uploadFileRequirements.Surname,
                StreetAddress = uploadFileRequirements.StreetAddress,
                PostalCode = uploadFileRequirements.PostalCode,
                PostalOffice = uploadFileRequirements.PostalOffice,
                MobilePhoneNumber = uploadFileRequirements.MobilePhoneNumber,
                Email = uploadFileRequirements.Email
            };

            NewDocumentFile fileInput = new ()
            {
                Title = $"Signert avtale {uploadFileRequirements.SequenceNumber}",
                Format = "pdf",
                Note = Utility.CreateChecksum(uploadFileRequirements.FileContent!),
                Base64Data = uploadFileRequirements.FileContent!
            };
            try
            {
                const string TemplateId = "toa";
                ICaseBuilder builder = _caseFactory.CreateBuilderForTemplate(TemplateId);
                builder.Synchronize();
                builder.UpdateToPreferredState();
                builder.AttachTo(caseOwner);
                IDocumentBuilder documentBuilder = builder.AddDocument(TemplateId);
                documentBuilder
                    .AddFile(fileInput).OnlyAddFileIf(NoExistingFilesMatchingNote).SignOff()
                    .AddPrivatePersonAsContact("Avsender", caseOwner, false, null);
                if (!string.IsNullOrEmpty(uploadFileRequirements.CaseManagerId ?? uploadFileRequirements.CaseManagerEmail))
                {
                    UniqueQueryAttributesTemplate? responsibleTemplate = _options.Responsible;
                    string? externalId = responsibleTemplate?.ExternalId;
                    string? email = responsibleTemplate?.Email;

                    ContactPerson personReference = new()
                    {
                        ExternalId = externalId?.Replace("{dfo.caseManager.id}", uploadFileRequirements.CaseManagerId!)?.Replace("{dfo.caseManager.email}", uploadFileRequirements.CaseManagerEmail!),
                        Email = email?.Replace("{dfo.caseManager.id}", uploadFileRequirements.CaseManagerId!)?.Replace("{dfo.caseManager.email}", uploadFileRequirements.CaseManagerEmail!)
                    };
                    documentBuilder.AddContactPersonAsContact("Mottaker", personReference, false, null);
                }
                if (uploadFileRequirements.SignedDate.HasValue) documentBuilder.WithDocumentDate(uploadFileRequirements.SignedDate.Value);
                await builder.Build().SubmitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload contract to Public 360");
                throw;
            }
        }

        private Task<bool> NoExistingFilesMatchingNote(NewDocumentFile fileToUpload, IEnumerable<P360Client.DTO.File> existingFiles)
        {
            return Task.FromResult(existingFiles.All(ef => ef.Note != fileToUpload.Note));
        }
    }
}
