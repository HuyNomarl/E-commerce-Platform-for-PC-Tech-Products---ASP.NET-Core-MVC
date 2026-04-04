using Eshop.Constants;
using Eshop.Helpers;
using Eshop.Hubs;
using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Services
{
    public class SupportChatService : ISupportChatService
    {
        private const long MaxImageSizeBytes = 5 * 1024 * 1024;
        private const long MaxVideoSizeBytes = 50 * 1024 * 1024;

        private readonly DataContext _context;
        private readonly UserManager<AppUserModel> _userManager;
        private readonly ICloudinaryService _cloudinaryService;
        private readonly IHubContext<ChatHub> _chatHubContext;
        private readonly IHubContext<NotificationHub> _notificationHubContext;

        public SupportChatService(
            DataContext context,
            UserManager<AppUserModel> userManager,
            ICloudinaryService cloudinaryService,
            IHubContext<ChatHub> chatHubContext,
            IHubContext<NotificationHub> notificationHubContext)
        {
            _context = context;
            _userManager = userManager;
            _cloudinaryService = cloudinaryService;
            _chatHubContext = chatHubContext;
            _notificationHubContext = notificationHubContext;
        }

        public async Task<SupportChatMessagePayloadViewModel> SendAsync(
            string senderId,
            string? receiverId,
            SupportChatSendInputViewModel input)
        {
            var sender = await _context.Users.FirstOrDefaultAsync(x => x.Id == senderId)
                ?? throw new InvalidOperationException("Nguoi gui khong ton tai.");

            var senderRoles = await _userManager.GetRolesAsync(sender);
            bool senderIsSupport = senderRoles.Any(RoleNames.IsSupportRole);
            bool senderIsCustomer = senderRoles.Any(role =>
                    string.Equals(role, RoleNames.Customer, StringComparison.OrdinalIgnoreCase))
                && !senderRoles.Any(RoleNames.IsBackOfficeRole);

            if (!senderIsSupport && !senderIsCustomer)
            {
                throw new InvalidOperationException("Tai khoan nay khong duoc phep su dung kenh chat ho tro.");
            }

            if (input.Attachment != null && input.ProductId.HasValue)
            {
                throw new InvalidOperationException("Moi tin nhan chi duoc gui mot media hoac mot san pham.");
            }

            var receiver = senderIsCustomer
                ? await ResolveReceiverForCustomerAsync(senderId, receiverId)
                : await ResolveReceiverForSupportAsync(receiverId);

            var receiverRoles = await _userManager.GetRolesAsync(receiver);
            bool receiverIsSupport = receiverRoles.Any(RoleNames.IsSupportRole);
            bool receiverIsCustomer = receiverRoles.Any(role =>
                    string.Equals(role, RoleNames.Customer, StringComparison.OrdinalIgnoreCase))
                && !receiverRoles.Any(RoleNames.IsBackOfficeRole);

            if (senderIsSupport && !receiverIsCustomer)
            {
                throw new InvalidOperationException("Nhan vien ho tro chi duoc chat voi khach hang.");
            }

            if (senderIsCustomer && !receiverIsSupport)
            {
                throw new InvalidOperationException("Khach hang chi duoc chat voi bo phan ho tro.");
            }

            var uploadedResources = new List<(string PublicId, string ResourceType)>();

            try
            {
                var storedContent = await BuildStoredContentAsync(input, uploadedResources);
                if (!SupportChatContentHelper.HasMeaningfulContent(storedContent))
                {
                    throw new InvalidOperationException("Tin nhan dang trong.");
                }

                var message = new MessageModel
                {
                    SenderId = sender.Id,
                    ReceiverId = receiver.Id,
                    Content = SupportChatContentHelper.Serialize(storedContent),
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                };

                _context.Messages.Add(message);
                await _context.SaveChangesAsync();

                var payload = BuildPayload(message, sender, receiver, storedContent, senderIsSupport);

                await _chatHubContext.Clients.User(sender.Id).SendAsync("ReceiveSupportMessage", payload);
                await _chatHubContext.Clients.User(receiver.Id).SendAsync("ReceiveSupportMessage", payload);
                await _chatHubContext.Clients.Group(NotificationGroups.SupportAgents)
                    .SendAsync("ReceiveSupportInboxMessage", payload);

                if (senderIsCustomer)
                {
                    await _notificationHubContext.Clients
                        .Group(NotificationGroups.SupportAgents)
                        .SendAsync("NewChatNotification", new
                        {
                            fromUserId = sender.Id,
                            fromUserName = ResolveDisplayName(sender),
                            preview = payload.Preview,
                            createdAt = payload.CreatedAt,
                            url = "/Admin/Chat"
                        });
                }

                return payload;
            }
            catch
            {
                foreach (var uploaded in uploadedResources)
                {
                    try
                    {
                        await _cloudinaryService.DeleteAsync(uploaded.PublicId, uploaded.ResourceType);
                    }
                    catch
                    {
                    }
                }

                throw;
            }
        }

        public async Task<List<SupportChatMessagePayloadViewModel>> GetConversationForCustomerAsync(string customerId)
        {
            var supportIds = await GetSupportUserIdsAsync();
            var messages = await _context.Messages
                .AsNoTracking()
                .Where(x =>
                    (x.SenderId == customerId && supportIds.Contains(x.ReceiverId)) ||
                    (x.ReceiverId == customerId && supportIds.Contains(x.SenderId)))
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            return await BuildPayloadsAsync(messages, supportIds);
        }

        public async Task<List<SupportChatMessagePayloadViewModel>> GetConversationForSupportAsync(string customerId)
        {
            var supportIds = await GetSupportUserIdsAsync();
            var messages = await _context.Messages
                .AsNoTracking()
                .Where(x =>
                    (x.SenderId == customerId && supportIds.Contains(x.ReceiverId)) ||
                    (x.ReceiverId == customerId && supportIds.Contains(x.SenderId)))
                .OrderBy(x => x.CreatedAt)
                .ToListAsync();

            return await BuildPayloadsAsync(messages, supportIds);
        }

        public async Task MarkConversationReadForCustomerAsync(string customerId)
        {
            var supportIds = await GetSupportUserIdsAsync();
            var unreadMessages = await _context.Messages
                .Where(x =>
                    x.ReceiverId == customerId &&
                    supportIds.Contains(x.SenderId) &&
                    !x.IsRead)
                .ToListAsync();

            if (!unreadMessages.Any())
            {
                return;
            }

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task MarkConversationReadForSupportAsync(string customerId)
        {
            var supportIds = await GetSupportUserIdsAsync();
            var unreadMessages = await _context.Messages
                .Where(x =>
                    x.SenderId == customerId &&
                    supportIds.Contains(x.ReceiverId) &&
                    !x.IsRead)
                .ToListAsync();

            if (!unreadMessages.Any())
            {
                return;
            }

            foreach (var message in unreadMessages)
            {
                message.IsRead = true;
            }

            await _context.SaveChangesAsync();
        }

        public async Task<List<SupportChatConversationItemViewModel>> GetSupportConversationsAsync()
        {
            var supportIds = await GetSupportUserIdsAsync();
            var messages = await _context.Messages
                .AsNoTracking()
                .Where(x => supportIds.Contains(x.SenderId) || supportIds.Contains(x.ReceiverId))
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();

            if (!messages.Any())
            {
                return new List<SupportChatConversationItemViewModel>();
            }

            var conversations = messages
                .GroupBy(x => supportIds.Contains(x.SenderId) ? x.ReceiverId : x.SenderId)
                .ToList();

            var customerIds = conversations
                .Select(x => x.Key)
                .Distinct()
                .ToList();

            var customerLookup = await _context.Users
                .AsNoTracking()
                .Where(x => customerIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            return conversations
                .Select(group =>
                {
                    var lastMessage = group.First();
                    var customerId = group.Key;
                    customerLookup.TryGetValue(customerId, out var customer);
                    var storedContent = SupportChatContentHelper.Deserialize(lastMessage.Content);

                    return new SupportChatConversationItemViewModel
                    {
                        Id = customerId,
                        UserName = customer == null ? "Khach hang" : ResolveDisplayName(customer),
                        Email = customer?.Email,
                        LastMessagePreview = SupportChatContentHelper.BuildPreview(storedContent),
                        LastMessageAt = lastMessage.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                        LastMessageAtIso = lastMessage.CreatedAt.ToString("o"),
                        UnreadCount = group.Count(x =>
                            !x.IsRead &&
                            x.SenderId == customerId &&
                            supportIds.Contains(x.ReceiverId))
                    };
                })
                .OrderByDescending(x => x.LastMessageAtIso)
                .ToList();
        }

        public async Task<List<SupportChatProductCardViewModel>> SearchProductsAsync(string? keyword, int limit = 6)
        {
            var normalizedKeyword = keyword?.Trim();

            var query = _context.Products
                .AsNoTracking()
                .Include(x => x.ProductImages)
                .WhereVisibleOnStorefront(_context)
                .OrderByDescending(x => x.Sold)
                .ThenByDescending(x => x.Id)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                query = query.Where(x =>
                    x.Name.Contains(normalizedKeyword) ||
                    x.Description.Contains(normalizedKeyword));
            }

            var products = await query
                .Take(Math.Max(1, limit))
                .ToListAsync();

            return products
                .Select(SupportChatContentHelper.BuildProductCard)
                .ToList();
        }

        public async Task<AppUserModel?> GetPreferredSupportUserAsync(string? customerId = null)
        {
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                var supportIds = await GetSupportUserIdsAsync();
                var lastSupportId = await _context.Messages
                    .AsNoTracking()
                    .Where(x =>
                        (x.SenderId == customerId && supportIds.Contains(x.ReceiverId)) ||
                        (x.ReceiverId == customerId && supportIds.Contains(x.SenderId)))
                    .OrderByDescending(x => x.CreatedAt)
                    .Select(x => x.SenderId == customerId ? x.ReceiverId : x.SenderId)
                    .FirstOrDefaultAsync();

                if (!string.IsNullOrWhiteSpace(lastSupportId))
                {
                    var lastSupportUser = await _context.Users.FirstOrDefaultAsync(x => x.Id == lastSupportId);
                    if (lastSupportUser != null)
                    {
                        return lastSupportUser;
                    }
                }
            }

            foreach (var roleName in new[] { RoleNames.SupportStaff, RoleNames.Admin })
            {
                var users = await _userManager.GetUsersInRoleAsync(roleName);
                var supportUser = users
                    .OrderBy(x => x.UserName)
                    .ThenBy(x => x.Email)
                    .FirstOrDefault();

                if (supportUser != null)
                {
                    return supportUser;
                }
            }

            return null;
        }

        private async Task<List<SupportChatMessagePayloadViewModel>> BuildPayloadsAsync(
            List<MessageModel> messages,
            List<string> supportIds)
        {
            if (!messages.Any())
            {
                return new List<SupportChatMessagePayloadViewModel>();
            }

            var userIds = messages
                .SelectMany(x => new[] { x.SenderId, x.ReceiverId })
                .Distinct()
                .ToList();

            var userLookup = await _context.Users
                .AsNoTracking()
                .Where(x => userIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            return messages
                .Select(message =>
                {
                    userLookup.TryGetValue(message.SenderId, out var sender);
                    userLookup.TryGetValue(message.ReceiverId, out var receiver);
                    var storedContent = SupportChatContentHelper.Deserialize(message.Content);

                    return BuildPayload(
                        message,
                        sender,
                        receiver,
                        storedContent,
                        supportIds.Contains(message.SenderId));
                })
                .ToList();
        }

        private SupportChatMessagePayloadViewModel BuildPayload(
            MessageModel message,
            AppUserModel? sender,
            AppUserModel? receiver,
            SupportChatStoredContentViewModel storedContent,
            bool senderIsSupport)
        {
            var preview = SupportChatContentHelper.BuildPreview(storedContent);
            var product = storedContent.ProductId.HasValue
                ? new SupportChatProductCardViewModel
                {
                    Id = storedContent.ProductId.Value,
                    Name = storedContent.ProductName ?? "San pham",
                    Price = storedContent.ProductPrice ?? 0,
                    ImageUrl = storedContent.ProductImageUrl,
                    Url = SupportChatContentHelper.ResolveProductUrl(storedContent.ProductId.Value)
                }
                : null;

            return new SupportChatMessagePayloadViewModel
            {
                Id = message.Id,
                SenderId = message.SenderId,
                SenderName = sender == null ? "Nguoi dung" : ResolveDisplayName(sender),
                ReceiverId = message.ReceiverId,
                ReceiverName = receiver == null ? "Nguoi dung" : ResolveDisplayName(receiver),
                MessageType = storedContent.MessageType,
                Text = storedContent.Text,
                Content = !string.IsNullOrWhiteSpace(storedContent.Text) ? storedContent.Text : preview,
                Preview = preview,
                CreatedAt = message.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                CreatedAtIso = message.CreatedAt.ToString("o"),
                IsFromSupport = senderIsSupport,
                AttachmentUrl = storedContent.AttachmentUrl,
                AttachmentName = storedContent.AttachmentFileName,
                AttachmentContentType = storedContent.AttachmentContentType,
                Product = product
            };
        }

        private async Task<SupportChatStoredContentViewModel> BuildStoredContentAsync(
            SupportChatSendInputViewModel input,
            List<(string PublicId, string ResourceType)> uploadedResources)
        {
            var storedContent = new SupportChatStoredContentViewModel
            {
                Text = string.IsNullOrWhiteSpace(input.Content) ? null : input.Content.Trim()
            };

            if (input.ProductId.HasValue)
            {
                var product = await _context.Products
                    .AsNoTracking()
                    .Include(x => x.ProductImages)
                    .WhereVisibleOnStorefront(_context)
                    .FirstOrDefaultAsync(x => x.Id == input.ProductId.Value)
                    ?? throw new InvalidOperationException("San pham khong ton tai.");

                var card = SupportChatContentHelper.BuildProductCard(product);
                storedContent.ProductId = card.Id;
                storedContent.ProductName = card.Name;
                storedContent.ProductPrice = card.Price;
                storedContent.ProductImageUrl = card.ImageUrl;
            }

            if (input.Attachment != null && input.Attachment.Length > 0)
            {
                var attachment = input.Attachment;
                if (attachment.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    if (attachment.Length > MaxImageSizeBytes)
                    {
                        throw new InvalidOperationException("Moi anh trong chat chi duoc toi da 5MB.");
                    }

                    var uploaded = await _cloudinaryService.UploadImageAsync(attachment, "eshop/chat/images");
                    uploadedResources.Add((uploaded.PublicId, uploaded.ResourceType));
                    storedContent.AttachmentUrl = uploaded.Url;
                    storedContent.AttachmentPublicId = uploaded.PublicId;
                    storedContent.AttachmentFileName = attachment.FileName;
                    storedContent.AttachmentContentType = attachment.ContentType;
                }
                else if (attachment.ContentType.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
                {
                    if (attachment.Length > MaxVideoSizeBytes)
                    {
                        throw new InvalidOperationException("Moi video trong chat chi duoc toi da 50MB.");
                    }

                    var uploaded = await _cloudinaryService.UploadVideoAsync(attachment, "eshop/chat/videos");
                    uploadedResources.Add((uploaded.PublicId, uploaded.ResourceType));
                    storedContent.AttachmentUrl = uploaded.Url;
                    storedContent.AttachmentPublicId = uploaded.PublicId;
                    storedContent.AttachmentFileName = attachment.FileName;
                    storedContent.AttachmentContentType = attachment.ContentType;
                }
                else
                {
                    throw new InvalidOperationException("Chat chi ho tro anh hoac video.");
                }
            }

            storedContent.MessageType = SupportChatContentHelper.ResolveMessageType(storedContent);
            return storedContent;
        }

        private async Task<AppUserModel> ResolveReceiverForCustomerAsync(string customerId, string? requestedReceiverId)
        {
            if (!string.IsNullOrWhiteSpace(requestedReceiverId))
            {
                var requestedReceiver = await _context.Users.FirstOrDefaultAsync(x => x.Id == requestedReceiverId);
                if (requestedReceiver != null)
                {
                    var roles = await _userManager.GetRolesAsync(requestedReceiver);
                    if (roles.Any(RoleNames.IsSupportRole))
                    {
                        return requestedReceiver;
                    }
                }
            }

            return await GetPreferredSupportUserAsync(customerId)
                ?? throw new InvalidOperationException("Chua co tai khoan ho tro.");
        }

        private async Task<AppUserModel> ResolveReceiverForSupportAsync(string? receiverId)
        {
            if (string.IsNullOrWhiteSpace(receiverId))
            {
                throw new InvalidOperationException("Khong co nguoi nhan.");
            }

            return await _context.Users.FirstOrDefaultAsync(x => x.Id == receiverId)
                ?? throw new InvalidOperationException("Nguoi nhan khong ton tai.");
        }

        private async Task<List<string>> GetSupportUserIdsAsync()
        {
            var supportIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var roleName in new[] { RoleNames.SupportStaff, RoleNames.Admin })
            {
                var users = await _userManager.GetUsersInRoleAsync(roleName);
                foreach (var user in users)
                {
                    supportIds.Add(user.Id);
                }
            }

            return supportIds.ToList();
        }

        private static string ResolveDisplayName(AppUserModel user)
        {
            return string.IsNullOrWhiteSpace(user.UserName)
                ? user.Email ?? "Nguoi dung"
                : user.UserName;
        }
    }
}
