using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using RoomToRentRadar.Adapters.Inbound.WebApi.Controllers;
using RoomToRentRadar.Application.DTOs;
using RoomToRentRadar.Application.Ports.Inbound;
using Xunit;
using Assert = Xunit.Assert;

namespace RoomToRentRadar.Adapters.Tests;

public class RoomsControllerTests
{
    private readonly Mock<IGetRoomDetailsUseCase> _mockUseCase;
    private readonly RoomsController _controller;

    public RoomsControllerTests()
    {
        _mockUseCase = new Mock<IGetRoomDetailsUseCase>();
        _controller = new RoomsController(_mockUseCase.Object);
    }

    [Fact]
    public async Task GetById_Case1_Anonymous_ShouldHideLandlordPhone()
    {
        // Case 1: Anonymous (Không đăng nhập) -> isUserVerified = false -> Số điện thoại bị ẩn.
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        _mockUseCase
            .Setup(u => u.ExecuteAsync("room-01", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomDetailsDto
            {
                Id = "room-01",
                LandlordPhone = "*** (Yêu cầu xác thực để xem)"
            });

        var result = await _controller.GetById("room-01", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RoomDetailsDto>(okResult.Value);
        Assert.Equal("*** (Yêu cầu xác thực để xem)", dto.LandlordPhone);
        _mockUseCase.Verify(u => u.ExecuteAsync("room-01", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_Case2_AuthenticatedButUnverified_ShouldHideLandlordPhone()
    {
        // Case 2: Authenticated but Unverified (Đăng nhập nhưng thiếu claim "IsVerified") -> isUserVerified = false -> Số điện thoại bị ẩn.
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "testuser"),
            new Claim("IsVerified", "false")
        }, "TestAuth");

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        _mockUseCase
            .Setup(u => u.ExecuteAsync("room-01", false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomDetailsDto
            {
                Id = "room-01",
                LandlordPhone = "*** (Yêu cầu xác thực để xem)"
            });

        var result = await _controller.GetById("room-01", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RoomDetailsDto>(okResult.Value);
        Assert.Equal("*** (Yêu cầu xác thực để xem)", dto.LandlordPhone);
        _mockUseCase.Verify(u => u.ExecuteAsync("room-01", false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetById_Case3_AuthenticatedAndVerified_ShouldReturnCorrectLandlordPhone()
    {
        // Case 3: Authenticated and Verified (Đăng nhập và có claim "IsVerified" = true) -> isUserVerified = true -> Trả về đúng số điện thoại.
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.Name, "verifieduser"),
            new Claim("IsVerified", "true")
        }, "TestAuth");

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(identity)
        };
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        _mockUseCase
            .Setup(u => u.ExecuteAsync("room-01", true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoomDetailsDto
            {
                Id = "room-01",
                LandlordPhone = "0901234567"
            });

        var result = await _controller.GetById("room-01", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var dto = Assert.IsType<RoomDetailsDto>(okResult.Value);
        Assert.Equal("0901234567", dto.LandlordPhone);
        _mockUseCase.Verify(u => u.ExecuteAsync("room-01", true, It.IsAny<CancellationToken>()), Times.Once);
    }
}
