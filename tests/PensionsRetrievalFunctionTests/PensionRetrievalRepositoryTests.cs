using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Models.MHPDModels;
using MhpdCommon.Repository;
using Microsoft.Extensions.Logging;
using Moq;
using PensionsRetrievalFunction.Repository;

namespace PensionsRetrievalFunctionTests;

public class PensionRetrievalRepositoryTests
{
    private readonly PensionRetrievalRepository _repository;
    private readonly Mock<IHashRedisRepository<PensionsRetrievalRecord>> _mockPensionsRetrievalRecordRepository;
    public PensionRetrievalRepositoryTests()
    {
        _mockPensionsRetrievalRecordRepository = new Mock<IHashRedisRepository<PensionsRetrievalRecord>>();
        _repository = new PensionRetrievalRepository(Mock.Of<ILogger<PensionRetrievalRepository>>(), _mockPensionsRetrievalRecordRepository.Object);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 0, false)]
    public async Task WhenRecordIsQueried_ReturnsNullOrNew(int recordsFound, int expectedCalls, bool isObjectReturned)
    {
        //Arrange
        var message = new PensionRetrievalPayload
        {
            UserSessionId = "Id",
            Iss = "iss",
            PeisId = "PeisId"
        };
        if (recordsFound > 0)
        {
            _mockPensionsRetrievalRecordRepository.Setup(mock => mock.GetByUserSessionIdAsync(It.IsAny<string>())).ReturnsAsync(new PensionsRetrievalRecord());
        }

        //Act
        var result = await _repository.CreateRecordIfNotExistsAsync(message);

        //Assert
        Assert.Equal(isObjectReturned, result != null);
        _mockPensionsRetrievalRecordRepository.Verify(mock => mock.InsertItemAsync(It.IsAny<PensionsRetrievalRecord>()), Times.Exactly(expectedCalls));
    }

    [Fact]
    public async Task WhenRecordIsProvided_DatabaseIsUpdated()
    {
        //Arrange
        var record = new PensionsRetrievalRecord();

        //Act
        await _repository.UpdatePensionsRetrievalRecordAsync(record);

        //Assert
        _mockPensionsRetrievalRecordRepository.Verify(mock => mock.InsertItemAsync(It.IsAny<PensionsRetrievalRecord>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenRecordIsRequested_DatabaseResultIsCorrect(bool isRecordInDatabase)
    {
        //Arrange
        List<PensionsRetrievalRecord> records = [];

        if (isRecordInDatabase)
        {

            _mockPensionsRetrievalRecordRepository.Setup(mock => mock.GetByUserSessionIdAsync(It.IsAny<string>())).ReturnsAsync(new PensionsRetrievalRecord());
        }

        //Act
        var record = await _repository.GetRetrievalRecordAsync(Guid.NewGuid().ToString());

        //Assert
        Assert.Equal(isRecordInDatabase, record != null);

    }

    [Fact]
    public async Task DeleteRetrievalRecordsAsync_ShouldCallDeleteAllItemsByPartitionKeyStreamAsyncWithPartitionKey()
    {
        //Arrange
        var userSessionId = Guid.NewGuid().ToString();

        //Act
        await _repository.DeleteRetrievalRecordsAsync(userSessionId);

        //Assert
        _mockPensionsRetrievalRecordRepository.Verify(r => r.DeleteByIdUserSessionIdAsync(userSessionId), Times.Once);
    }
}