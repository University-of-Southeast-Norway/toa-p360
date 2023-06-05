using Microsoft.Extensions.Options;
using P360Client.Domain;
using P360Client.Domain.Configurations;
using P360Client.Domain.Model;
using ToaArchiver.Domain.Core;
using ToaArchiver.Domain.DTO;

namespace ToaArchiver.Archives.P360
{
    public class P360Archive : IArchive
    {
        private readonly IAppendCase _caseAdder;

        private readonly IOptions<AppendCaseOptions> _options;

        public P360Archive(IAppendCase caseAdder, IOptions<AppendCaseOptions> options)
        {
            _caseAdder = caseAdder;
            _options = options;
        }

        public void UploadSignedContract(SignedContractData uploadFileRequirements)
        {
            throw new NotImplementedException($"{nameof(P360Archive)} is based on async logic.");
        }

        public async Task UploadSignedContractAsync(SignedContractData uploadFileRequirements)
        {
            PrivatePerson contactReference = new()
            {
                SSN = uploadFileRequirements.SocialSecurityNumber,
                FirstName = uploadFileRequirements.FirstName,
                MiddleName = uploadFileRequirements.MiddleName,
                Surname = uploadFileRequirements.Surname,
                StreetAddress = uploadFileRequirements.StreetAddress,
                PostalCode = uploadFileRequirements.PostalCode,
                PostalOffice = uploadFileRequirements.PostalOffice,
                MobilePhoneNumber = uploadFileRequirements.MobilePhoneNumber,
                Email = uploadFileRequirements.Email
            };

            FileContent fileInput = new ()
            {
                Title = $"Signert avtale {uploadFileRequirements.SequenceNumber}",
                Format = "pdf",
                Note = Utility.CreateChecksum(uploadFileRequirements.FileContent),
                Base64FileContent = uploadFileRequirements.FileContent
            };

            await _caseAdder.AppendCase(personCase =>
                    personCase.CreatedDate > _options.Value.InProductionDate &&
                    (personCase.Status == "Under behandling" || personCase.Status == "In progress"))
                .AddContactReference("Sakspart", contactReference)
                .AppendDocument(fileInput)
                .AddContactReference("Avsender", contactReference)
                .BuildAsync();
        }
    }
}
