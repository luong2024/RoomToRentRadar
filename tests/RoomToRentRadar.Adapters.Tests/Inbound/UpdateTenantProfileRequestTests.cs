using System.Text.Json;
using RoomToRentRadar.Adapters.Inbound.WebApi.Contracts;

namespace RoomToRentRadar.Adapters.Tests.Inbound;

[TestFixture]
public class UpdateTenantProfileRequestTests
{
    [Test]
    public void Deserialize_WhenOptionalFieldsAreOmitted_MarksThemAsUnspecified()
    {
        var request = JsonSerializer.Deserialize<UpdateTenantProfileRequest>(
            """{ "fullName": "Student" }""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Multiple(() =>
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.IsStudentIdSpecified, Is.False);
            Assert.That(request.IsUniversitySpecified, Is.False);
            Assert.That(request.IsFacultySpecified, Is.False);
            Assert.That(request.IsMaxBudgetSpecified, Is.False);
        });
    }

    [Test]
    public void Deserialize_WhenOptionalFieldsAreNull_MarksThemAsSpecified()
    {
        var request = JsonSerializer.Deserialize<UpdateTenantProfileRequest>(
            """{ "fullName": "Student", "studentId": null, "university": null, "faculty": null, "maxBudget": null }""",
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.Multiple(() =>
        {
            Assert.That(request, Is.Not.Null);
            Assert.That(request!.IsStudentIdSpecified, Is.True);
            Assert.That(request.IsUniversitySpecified, Is.True);
            Assert.That(request.IsFacultySpecified, Is.True);
            Assert.That(request.IsMaxBudgetSpecified, Is.True);
        });
    }
}
