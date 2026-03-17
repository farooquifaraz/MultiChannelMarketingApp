using System.Text.Json;
using AutoMapper;
using MarketingApp.Application.DTOs;
using MarketingApp.Domain.Entities;

namespace MarketingApp.Application.Mappings;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        // User
        CreateMap<User, UserDto>();

        // Contact
        CreateMap<Contact, ContactDto>()
            .ForMember(d => d.GroupName, opt => opt.MapFrom(s => s.Group != null ? s.Group.Name : null))
            .ForMember(d => d.CustomFields, opt => opt.MapFrom(s => DeserializeJson(s.CustomFields)));
        CreateMap<CreateContactDto, Contact>()
            .ForMember(d => d.CustomFields, opt => opt.Ignore());
        CreateMap<UpdateContactDto, Contact>()
            .ForMember(d => d.CustomFields, opt => opt.Ignore());

        // Template
        CreateMap<MessageTemplate, TemplateDto>();
        CreateMap<CreateTemplateDto, MessageTemplate>();
        CreateMap<UpdateTemplateDto, MessageTemplate>();

        // Campaign
        CreateMap<Campaign, CampaignDto>()
            .ForMember(d => d.TemplateName, opt => opt.MapFrom(s => s.Template != null ? s.Template.Name : null))
            .ForMember(d => d.GroupName, opt => opt.MapFrom(s => s.Group != null ? s.Group.Name : null));
        CreateMap<Campaign, CampaignDetailDto>()
            .ForMember(d => d.TemplateName, opt => opt.MapFrom(s => s.Template != null ? s.Template.Name : null))
            .ForMember(d => d.GroupName, opt => opt.MapFrom(s => s.Group != null ? s.Group.Name : null));
        CreateMap<CreateCampaignDto, Campaign>();
        CreateMap<UpdateCampaignDto, Campaign>();

        // Campaign Message
        CreateMap<CampaignMessage, CampaignMessageDto>()
            .ForMember(d => d.ContactName, opt => opt.MapFrom(s => s.Contact.FullName))
            .ForMember(d => d.ContactEmail, opt => opt.MapFrom(s => s.Contact.Email))
            .ForMember(d => d.ContactPhone, opt => opt.MapFrom(s => s.Contact.Phone));

        // SmtpSettings mappings
        CreateMap<UserSmtpSettings, SmtpSettingsDto>();

        // Notification mappings
        CreateMap<Notification, NotificationDto>();
    }

    private static Dictionary<string, string>? DeserializeJson(string? json)
    {
        if (string.IsNullOrEmpty(json)) return null;
        try { return JsonSerializer.Deserialize<Dictionary<string, string>>(json); }
        catch { return null; }
    }
}
