using MhpdCommon.Models.Configuration;
using MhpdCommon.Models.MessageBodyModels;
using MhpdCommon.Models.MHPDModels;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Moq;
using PensionsRetrievalFunction.Repository;

namespace PensionsRetrievalFunctionTests;

public class PensionRetrievalRepositoryTests
{
    private readonly Mock<Container> _container;
    private readonly PensionRetrievalRepository _repository;
    private readonly Mock<FeedResponse<PensionsRetrievalRecord>> _readResponse;
    private readonly Mock<ItemResponse<PensionsRetrievalRecord>> _writeResponse;

    public PensionRetrievalRepositoryTests()
    {
        var configuration = new CosmosBusinessConfiguration
        {
            DatabaseId = "PensionDatabase",
            PensionsRetrievalContainer = "PensionContainer"
        };

        var options = Options.Create(configuration);
        var client = new Mock<CosmosClient>();
        var iterator = new Mock<FeedIterator<PensionsRetrievalRecord>>();
        _container = new Mock<Container>();
        _readResponse = new Mock<FeedResponse<PensionsRetrievalRecord>>();
        _writeResponse = new Mock<ItemResponse<PensionsRetrievalRecord>>();

        client.Setup(mock => mock.GetContainer(configuration.DatabaseId, configuration.PensionsRetrievalContainer))
            .Returns(_container.Object);

        _container.Setup(mock => mock.GetItemQueryIterator<PensionsRetrievalRecord>(It.IsAny<QueryDefinition>(),
            It.IsAny<string>(), It.IsAny<QueryRequestOptions>())).Returns(iterator.Object);
        _container.Setup(mock => mock.CreateItemAsync(It.IsAny<PensionsRetrievalRecord>(), It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(_writeResponse.Object).Verifiable();
        _container.Setup(mock => mock.ReplaceItemAsync(It.IsAny<PensionsRetrievalRecord>(), It.IsAny<string>(), It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(_writeResponse.Object).Verifiable();

        iterator.Setup(mock => mock.ReadNextAsync(It.IsAny<CancellationToken>())).ReturnsAsync(_readResponse.Object);

        _repository = new PensionRetrievalRepository(options, client.Object);
    }

    [Theory]
    [InlineData(0, 1, true)]
    [InlineData(1, 0, false)]
    public async Task WhenRecordIsQueried_ReturnsNullOrNew(int recordsFound, int expectedCalls, bool isObjectReturned)
    {
        //Arrange
        _container.Invocations.Clear();
        var message = new PensionRetrievalPayload
        {
            UserSessionId = "Id",
            Iss = "iss",
            PeisId = "PeisId"
        };

        _writeResponse.Setup(mock => mock.Resource).Returns(new PensionsRetrievalRecord());
        _readResponse.Setup(mock => mock.Count).Returns(recordsFound);

        //Act
        var result = await _repository.CreateRecordIfNotExistsAsync(message);

        //Assert
        Assert.Equal(isObjectReturned, result != null);
        _container.Verify(mock => mock.CreateItemAsync(It.IsAny<PensionsRetrievalRecord>(), It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Exactly(expectedCalls));
    }

    [Fact]
    public async Task WhenRecordIsProvided_DatabaseIsUpdated()
    {
        //Arrange
        var record = new PensionsRetrievalRecord();

        //Act
        await _repository.UpdatePensionsRetrievalRecordAsync(record);

        //Assert
        _container.Verify(mock => mock.ReplaceItemAsync(It.IsAny<PensionsRetrievalRecord>(), It.IsAny<string>(), It.IsAny<PartitionKey>(),
            It.IsAny<ItemRequestOptions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenRecordIsRequested_DatabaseResultIsCorrect(bool isRecordInDatabase)
    {
        //Arrange
        List<PensionsRetrievalRecord> records = [];

        if (isRecordInDatabase) records.Add(new PensionsRetrievalRecord());

        _readResponse.Setup(mock => mock.GetEnumerator()).Returns(records.GetEnumerator);

        //Act
        var record = await _repository.GetRetrievalRecordAsync(Guid.NewGuid().ToString());

        //Assert
        Assert.Equal(isRecordInDatabase, record != null);

    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task WhenRecordIsDeleted_DatabaseResultIsCorrect(bool isRecordInDatabase)
    {
        //Arrange
        List<PensionsRetrievalRecord> records = [];

        if (isRecordInDatabase)
        {
            records.Add(new PensionsRetrievalRecord());
            records.Add(new PensionsRetrievalRecord());
        }

        _readResponse.Setup(mock => mock.GetEnumerator()).Returns(records.GetEnumerator);
        _readResponse.Setup(mock => mock.Count).Returns(records.Count);

        //Act
        var count = await _repository.DeleteRetrievalRecordsAsync(Guid.NewGuid().ToString());

        //Assert
        Assert.Equal(records.Count, count);
    }
}