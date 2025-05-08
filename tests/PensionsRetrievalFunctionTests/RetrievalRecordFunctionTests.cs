using MhpdCommon.Constants;
using MhpdCommon.Models.MHPDModels;
using MhpdCommon.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using PensionsRetrievalFunction;
using PensionsRetrievalFunction.Models;
using PensionsRetrievalFunction.Repository;
using System.Net;

namespace PensionsRetrievalFunctionTests;
public class RetrievalRecordFunctionTests
{
    private readonly Mock<IIdValidator> _idValidatorMock;
    private readonly Mock<ILogger<RetrievalRecordFunction>> _loggerMock;
    private readonly Mock<IPensionRetrievalRepository> _repository;
    private readonly RetrievalRecordFunction _function;

    public RetrievalRecordFunctionTests()
    {
        _idValidatorMock = new Mock<IIdValidator>();
        _idValidatorMock.Setup(x => x.IsValidGuid(It.IsAny<string>())).Returns(true);

        _loggerMock = new Mock<ILogger<RetrievalRecordFunction>>();

        _repository = new Mock<IPensionRetrievalRepository>();
        _repository.Setup(mock => mock.GetRetrievalRecordAsync(It.IsAny<string>())).ReturnsAsync(new PensionsRetrievalRecord());
        _repository.Setup(mock => mock.DeleteRetrievalRecordsAsync(It.IsAny<string>())).ReturnsAsync(2);

        _function = new RetrievalRecordFunction(_loggerMock.Object, _repository.Object, _idValidatorMock.Object);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Function_ShouldReturnOk_WhenHeadersAreValid(bool withCorrelationHeader)
    {
        //Arrange
        var userSessionId = Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();
        var request = new DefaultHttpContext().Request;
        request.Headers[HeaderConstants.UserSessionId] = userSessionId;

        if (withCorrelationHeader)
        {
            request.Headers[HeaderConstants.CorrelationId] = correlationId;
        }

        var mockRequest = new Mock<HttpRequest>();
        mockRequest.Setup(req => req.Headers).Returns(request.Headers);

        //Act
        var response = await _function.GetAsync(mockRequest.Object);

        //Assert
        var result = Assert.IsType<OkObjectResult>(response);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.IsType<PensionsRetrievalRecord>(result.Value);
        _repository.Verify(mock => mock.GetRetrievalRecordAsync(userSessionId), Times.Once);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Function_ShouldReturnBadRequest_WhenSessionIdHeadersIsInvalid(bool withSessionId)
    {
        //Arrange
        var correlationId = Guid.NewGuid().ToString();
        _idValidatorMock.Setup(x => x.IsValidGuid(It.IsAny<string>())).Returns(false);
        _idValidatorMock.Setup(x => x.IsValidGuid(correlationId)).Returns(true);

        var request = new DefaultHttpContext().Request;

        request.Headers[HeaderConstants.CorrelationId] = correlationId;

        if (withSessionId)
        {
            request.Headers[HeaderConstants.UserSessionId] = Guid.NewGuid().ToString();
        }

        var mockRequest = new Mock<HttpRequest>();
        mockRequest.Setup(req => req.Headers).Returns(request.Headers);

        //Act
        var response = await _function.GetAsync(mockRequest.Object);

        //Assert
        var result = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(Constants.ResponseType.InvalidSessionId, result.Value);
        _repository.Verify(mock => mock.GetRetrievalRecordAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task Function_ShouldReturnBadRequest_WhenCorrelationIdHeadersIsInvalid()
    {
        //Arrange
        var correlationId = "Guid.NewGuid().ToString()";
        _idValidatorMock.Setup(x => x.IsValidGuid(It.IsAny<string>())).Returns(false);

        var request = new DefaultHttpContext().Request;

        request.Headers[HeaderConstants.CorrelationId] = correlationId;

        var mockRequest = new Mock<HttpRequest>();
        mockRequest.Setup(req => req.Headers).Returns(request.Headers);

        //Act
        var response = await _function.GetAsync(mockRequest.Object);

        //Assert
        var result = Assert.IsType<BadRequestObjectResult>(response);
        Assert.Equal((int)HttpStatusCode.BadRequest, result.StatusCode);
        Assert.Equal(Constants.ResponseType.InvalidCorrelationId, result.Value);
        _repository.Verify(mock => mock.GetRetrievalRecordAsync(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_ShouldReturnOk_WhenPayloadIsValid(bool withCorrelationHeader)
    {
        //Arrange
        var userSessionId = Guid.NewGuid().ToString();
        var correlationId = Guid.NewGuid().ToString();
        var request = new DefaultHttpContext().Request;
        request.Headers[HeaderConstants.UserSessionId] = userSessionId;

        if (withCorrelationHeader)
        {
            request.Headers[HeaderConstants.CorrelationId] = correlationId;
        }

        var mockRequest = new Mock<HttpRequest>();
        mockRequest.Setup(req => req.Headers).Returns(request.Headers);

        //Act
        var response = await _function.DeleteAsync(mockRequest.Object);

        //Assert
        var result = Assert.IsType<OkObjectResult>(response);
        Assert.Equal((int)HttpStatusCode.OK, result.StatusCode);
        Assert.IsType<int>(result.Value);
        _repository.Verify(mock => mock.DeleteRetrievalRecordsAsync(userSessionId), Times.Once);
    }
}
