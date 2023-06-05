using ToaArchiver.Domain.DTO;

namespace ToaArchiver.Domain.Core;

public interface IArchive
{
    void UploadSignedContract(SignedContractData uploadFileRequirements);
    Task UploadSignedContractAsync(SignedContractData uploadFileRequirements);
}
