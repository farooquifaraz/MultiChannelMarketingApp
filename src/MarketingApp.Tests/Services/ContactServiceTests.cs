using System.Linq.Expressions;
using AutoMapper;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using MarketingApp.Application.DTOs;
using MarketingApp.Application.Interfaces;
using MarketingApp.Application.Mappings;
using MarketingApp.Application.Services;
using MarketingApp.Domain.Entities;
using MarketingApp.Domain.Exceptions;

namespace MarketingApp.Tests.Services;

public class ContactServiceTests
{
    private readonly Mock<IContactRepository> _contactRepoMock;
    private readonly Mock<IGenericRepository<ContactGroup>> _groupRepoMock;
    private readonly Mock<IGenericRepository<User>> _userRepoMock;
    private readonly Mock<IGenericRepository<SmtpGroup>> _smtpGroupRepoMock;
    private readonly Mock<ISystemSettingsService> _systemSettingsMock;
    private readonly Mock<IAuditService> _auditMock;
    private readonly Mock<ILogger<ContactService>> _loggerMock;
    private readonly IMapper _mapper;
    private readonly ContactService _sut;

    public ContactServiceTests()
    {
        _contactRepoMock = new Mock<IContactRepository>();
        _groupRepoMock = new Mock<IGenericRepository<ContactGroup>>();
        _userRepoMock = new Mock<IGenericRepository<User>>();
        _smtpGroupRepoMock = new Mock<IGenericRepository<SmtpGroup>>();
        _systemSettingsMock = new Mock<ISystemSettingsService>();
        _auditMock = new Mock<IAuditService>();
        _loggerMock = new Mock<ILogger<ContactService>>();

        var config = new MapperConfiguration(cfg => cfg.AddProfile<MappingProfile>());
        _mapper = config.CreateMapper();

        _sut = new ContactService(
            _contactRepoMock.Object,
            _groupRepoMock.Object,
            _userRepoMock.Object,
            _smtpGroupRepoMock.Object,
            _systemSettingsMock.Object,
            _auditMock.Object,
            _mapper,
            _loggerMock.Object);
    }

    #region GetAllAsync

    [Fact]
    public async Task GetAllAsync_ReturnsPagedResponse()
    {
        var userId = Guid.NewGuid();
        var contacts = new List<Contact>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, FullName = "Alice", Email = "alice@test.com" },
            new() { Id = Guid.NewGuid(), UserId = userId, FullName = "Bob", Email = "bob@test.com" }
        };

        _contactRepoMock
            .Setup(r => r.GetPagedAsync(userId, 1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((contacts.AsEnumerable(), 2));

        var result = await _sut.GetAllAsync(userId, 1, 10, null, null, CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.PageNumber.Should().Be(1);
        result.PageSize.Should().Be(10);
        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_EmptyList_ReturnsEmptyData()
    {
        var userId = Guid.NewGuid();
        _contactRepoMock
            .Setup(r => r.GetPagedAsync(userId, 1, 10, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<Contact>(), 0));

        var result = await _sut.GetAllAsync(userId, 1, 10, null, null, CancellationToken.None);

        result.TotalCount.Should().Be(0);
        result.Data.Should().BeEmpty();
    }

    #endregion

    #region GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_WhenExists_ReturnsContact()
    {
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = new Contact { Id = contactId, UserId = userId, FullName = "Alice", Email = "alice@test.com" };

        _contactRepoMock
            .Setup(r => r.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        var result = await _sut.GetByIdAsync(contactId, userId, CancellationToken.None);

        result.Id.Should().Be(contactId);
        result.FullName.Should().Be("Alice");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ThrowsNotFoundException()
    {
        var contactId = Guid.NewGuid();
        _contactRepoMock
            .Setup(r => r.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        var act = () => _sut.GetByIdAsync(contactId, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task GetByIdAsync_WhenDifferentUser_ThrowsForbiddenException()
    {
        var contactId = Guid.NewGuid();
        var contact = new Contact { Id = contactId, UserId = Guid.NewGuid(), FullName = "Alice" };

        _contactRepoMock
            .Setup(r => r.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        var act = () => _sut.GetByIdAsync(contactId, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    #endregion

    #region CreateAsync

    [Fact]
    public async Task CreateAsync_ReturnsCreatedContact()
    {
        var userId = Guid.NewGuid();
        var dto = new CreateContactDto
        {
            FullName = "New Contact",
            Email = "new@test.com",
            Phone = "+123"
        };

        _contactRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact c, CancellationToken _) => c);

        var result = await _sut.CreateAsync(userId, dto, CancellationToken.None);

        result.FullName.Should().Be("New Contact");
        result.Email.Should().Be("new@test.com");
        _contactRepoMock.Verify(r => r.AddAsync(It.Is<Contact>(c => c.UserId == userId), It.IsAny<CancellationToken>()), Times.Once);
        _auditMock.Verify(a => a.LogAsync(userId, "ContactCreated", "Contact", It.IsAny<Guid>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithCustomFields_SerializesToJson()
    {
        var userId = Guid.NewGuid();
        var dto = new CreateContactDto
        {
            FullName = "With Fields",
            Email = "fields@test.com",
            CustomFields = new Dictionary<string, string> { { "company", "Acme" } }
        };

        Contact? savedContact = null;
        _contactRepoMock
            .Setup(r => r.AddAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()))
            .Callback<Contact, CancellationToken>((c, _) => savedContact = c)
            .ReturnsAsync((Contact c, CancellationToken _) => c);

        await _sut.CreateAsync(userId, dto, CancellationToken.None);

        savedContact.Should().NotBeNull();
        savedContact!.CustomFields.Should().Contain("Acme");
    }

    #endregion

    #region UpdateAsync

    [Fact]
    public async Task UpdateAsync_WhenValid_UpdatesAndReturns()
    {
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var existing = new Contact { Id = contactId, UserId = userId, FullName = "Old Name", Email = "old@test.com" };

        _contactRepoMock
            .Setup(r => r.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var updateDto = new UpdateContactDto { FullName = "New Name", Email = "new@test.com" };

        var result = await _sut.UpdateAsync(contactId, userId, updateDto, CancellationToken.None);

        result.FullName.Should().Be("New Name");
        _contactRepoMock.Verify(r => r.UpdateAsync(It.IsAny<Contact>(), It.IsAny<CancellationToken>()), Times.Once);
        _auditMock.Verify(a => a.LogAsync(userId, "ContactUpdated", "Contact", contactId, It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _contactRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        var act = () => _sut.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateContactDto { FullName = "X" }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenWrongUser_ThrowsForbiddenException()
    {
        var contact = new Contact { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), FullName = "X" };
        _contactRepoMock
            .Setup(r => r.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        var act = () => _sut.UpdateAsync(contact.Id, Guid.NewGuid(), new UpdateContactDto { FullName = "Y" }, CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    #endregion

    #region DeleteAsync

    [Fact]
    public async Task DeleteAsync_WhenValid_DeletesAndAudits()
    {
        var userId = Guid.NewGuid();
        var contactId = Guid.NewGuid();
        var contact = new Contact { Id = contactId, UserId = userId, FullName = "Delete Me" };

        _contactRepoMock
            .Setup(r => r.GetByIdAsync(contactId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        await _sut.DeleteAsync(contactId, userId, CancellationToken.None);

        _contactRepoMock.Verify(r => r.DeleteAsync(contact, It.IsAny<CancellationToken>()), Times.Once);
        _auditMock.Verify(a => a.LogAsync(userId, "ContactDeleted", "Contact", contactId, It.IsAny<object>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _contactRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Contact?)null);

        var act = () => _sut.DeleteAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenWrongUser_ThrowsForbiddenException()
    {
        var contact = new Contact { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), FullName = "X" };
        _contactRepoMock
            .Setup(r => r.GetByIdAsync(contact.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(contact);

        var act = () => _sut.DeleteAsync(contact.Id, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    #endregion

    #region CreateGroupAsync

    [Fact]
    public async Task CreateGroupAsync_ReturnsCreatedGroup()
    {
        var userId = Guid.NewGuid();
        var dto = new CreateContactGroupDto { Name = "VIP Contacts", Description = "Top clients" };

        _groupRepoMock
            .Setup(r => r.AddAsync(It.IsAny<ContactGroup>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactGroup g, CancellationToken _) => g);

        var result = await _sut.CreateGroupAsync(userId, dto, CancellationToken.None);

        result.Name.Should().Be("VIP Contacts");
        result.Description.Should().Be("Top clients");
    }

    #endregion

    #region DeleteGroupAsync

    [Fact]
    public async Task DeleteGroupAsync_WhenNotFound_ThrowsNotFoundException()
    {
        _groupRepoMock
            .Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContactGroup?)null);

        var act = () => _sut.DeleteGroupAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task DeleteGroupAsync_WhenWrongUser_ThrowsForbiddenException()
    {
        var group = new ContactGroup { Id = Guid.NewGuid(), UserId = Guid.NewGuid(), Name = "G" };
        _groupRepoMock
            .Setup(r => r.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        var act = () => _sut.DeleteGroupAsync(group.Id, Guid.NewGuid(), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task DeleteGroupAsync_WhenValid_Deletes()
    {
        var userId = Guid.NewGuid();
        var group = new ContactGroup { Id = Guid.NewGuid(), UserId = userId, Name = "G" };
        _groupRepoMock
            .Setup(r => r.GetByIdAsync(group.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(group);

        await _sut.DeleteGroupAsync(group.Id, userId, CancellationToken.None);

        _groupRepoMock.Verify(r => r.DeleteAsync(group, It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion
}
