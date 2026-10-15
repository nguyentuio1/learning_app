using System;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Xunit;

namespace SmartDroneDelivery.Tests;

public class RbacIntegrationTests : IClassFixture<AuthApiFactory>
{
    private readonly HttpClient _client;

    public RbacIntegrationTests(AuthApiFactory factory)
    {
        // 🛠️ ĐÁNH CHẶN CẤP HỆ THỐNG: 
        // Ép biến môi trường hệ thống của luồng chạy hiện tại thành "Testing"
        // Điều này đảm bảo dù Program.cs chạy trước hay sau, nó cũng bắt buộc phải đọc cấu hình Testing.
        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        _client = factory.CreateClient();
    }

    // Giữ nguyên toàn bộ các hàm [Fact] và [Theory] kiểm thử phân quyền bảo mật cũ của bạn bên dưới...
}
