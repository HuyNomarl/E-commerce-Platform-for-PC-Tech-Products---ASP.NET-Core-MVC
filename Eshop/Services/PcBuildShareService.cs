using Eshop.Constants;
using Eshop.Models;
using Eshop.Models.ViewModels;
using Eshop.Repository;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Services
{
    public class PcBuildShareService : IPcBuildShareService
    {
        private readonly DataContext _context;
        private readonly IPcBuildStorageService _pcBuildStorageService;

        public PcBuildShareService(
            DataContext context,
            IPcBuildStorageService pcBuildStorageService)
        {
            _context = context;
            _pcBuildStorageService = pcBuildStorageService;
        }

        public async Task<List<PcBuildShareUserLookupDto>> SearchReceiversAsync(string currentUserId, string? keyword, int limit = 8)
        {
            var normalizedKeyword = keyword?.Trim();
            var take = Math.Max(limit * 3, limit);

            var query = _context.Users
                .AsNoTracking()
                .Where(x => x.Id != currentUserId);

            if (!string.IsNullOrWhiteSpace(normalizedKeyword))
            {
                query = query.Where(x =>
                    (x.UserName != null && x.UserName.Contains(normalizedKeyword)) ||
                    (x.Email != null && x.Email.Contains(normalizedKeyword)));
            }

            var candidates = await query
                .OrderBy(x => x.UserName)
                .ThenBy(x => x.Email)
                .Take(take)
                .Select(x => new AppUserModel
                {
                    Id = x.Id,
                    UserName = x.UserName,
                    Email = x.Email
                })
                .ToListAsync();

            var userIds = candidates.Select(x => x.Id).ToList();
            if (!userIds.Any())
            {
                return new List<PcBuildShareUserLookupDto>();
            }

            var rolesById = await _context.Roles
                .AsNoTracking()
                .ToDictionaryAsync(x => x.Id, x => x.Name ?? string.Empty);

            var userRoles = await _context.UserRoles
                .AsNoTracking()
                .Where(x => userIds.Contains(x.UserId))
                .ToListAsync();

            var roleLookup = userRoles.ToLookup(
                x => x.UserId,
                x => rolesById.TryGetValue(x.RoleId, out var roleName) ? roleName : string.Empty);

            return candidates
                .Where(user =>
                {
                    var roleNames = roleLookup[user.Id]
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList();

                    return roleNames.Any(role => string.Equals(role, RoleNames.Customer, StringComparison.OrdinalIgnoreCase)) &&
                           !roleNames.Any(RoleNames.IsBackOfficeRole);
                })
                .Take(limit)
                .Select(user => new PcBuildShareUserLookupDto
                {
                    Id = user.Id,
                    UserName = user.UserName ?? "Khách hàng",
                    Email = user.Email
                })
                .ToList();
        }

        public async Task<PcBuildShareCreatedDto> ShareAsync(string senderUserId, PcBuildShareRequest request)
        {
            if (string.IsNullOrWhiteSpace(senderUserId))
            {
                throw new InvalidOperationException("Bạn cần đăng nhập để chia sẻ cấu hình.");
            }

            if (string.IsNullOrWhiteSpace(request.ReceiverId))
            {
                throw new InvalidOperationException("Bạn chưa chọn người nhận.");
            }

            if (string.Equals(senderUserId, request.ReceiverId, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Bạn không thể chia sẻ cấu hình cho chính mình.");
            }

            var receiver = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == request.ReceiverId);

            if (receiver == null)
            {
                throw new InvalidOperationException("Người nhận không tồn tại.");
            }

            var receiverRoleNames = await (
                from userRole in _context.UserRoles.AsNoTracking()
                join role in _context.Roles.AsNoTracking() on userRole.RoleId equals role.Id
                where userRole.UserId == receiver.Id
                select role.Name ?? string.Empty)
                .ToListAsync();

            if (!receiverRoleNames.Any(role => string.Equals(role, RoleNames.Customer, StringComparison.OrdinalIgnoreCase)) ||
                receiverRoleNames.Any(RoleNames.IsBackOfficeRole))
            {
                throw new InvalidOperationException("Người nhận không hợp lệ để chia sẻ cấu hình.");
            }

            var build = await _pcBuildStorageService.SaveAsync(
                request.BuildName,
                request.Items,
                senderUserId,
                allowInvalidBuild: true);

            var share = new PcBuildShareModel
            {
                PcBuildId = build.BuildId,
                SenderUserId = senderUserId,
                ReceiverUserId = request.ReceiverId.Trim(),
                Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim()
            };

            _context.Set<PcBuildShareModel>().Add(share);
            await _context.SaveChangesAsync();

            return new PcBuildShareCreatedDto
            {
                ShareCode = share.ShareCode,
                ReceiverName = receiver.UserName ?? receiver.Email ?? "Người nhận",
                BuildCode = build.BuildCode,
                BuildName = build.Detail.BuildName
            };
        }

        public async Task<List<PcBuildShareListItemDto>> GetReceivedSharesAsync(string receiverUserId, int take = 20)
        {
            var shares = await _context.Set<PcBuildShareModel>()
                .AsNoTracking()
                .Include(x => x.PcBuild)
                .Include(x => x.SenderUser)
                .Where(x => x.ReceiverUserId == receiverUserId)
                .OrderByDescending(x => x.CreatedAt)
                .Take(Math.Max(1, take))
                .ToListAsync();

            return shares
                .Select(x => new PcBuildShareListItemDto
                {
                    ShareCode = x.ShareCode,
                    BuildCode = x.PcBuild?.BuildCode ?? string.Empty,
                    BuildName = x.PcBuild?.BuildName ?? "PC Build",
                    SenderName = x.SenderUser?.UserName ?? x.SenderUser?.Email ?? "Người gửi",
                    SenderEmail = x.SenderUser?.Email,
                    Note = x.Note,
                    TotalPrice = x.PcBuild?.TotalPrice ?? 0,
                    CreatedAtUtc = x.CreatedAt,
                    CreatedAtText = x.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm"),
                    IsOpened = x.OpenedAt.HasValue
                })
                .ToList();
        }

        public async Task<PcBuilderBuildDetailDto?> GetSharedBuildAsync(string receiverUserId, string shareCode)
        {
            if (string.IsNullOrWhiteSpace(receiverUserId) || string.IsNullOrWhiteSpace(shareCode))
            {
                return null;
            }

            var share = await _context.Set<PcBuildShareModel>()
                .FirstOrDefaultAsync(x =>
                    x.ReceiverUserId == receiverUserId &&
                    x.ShareCode == shareCode.Trim());

            if (share == null)
            {
                return null;
            }

            if (!share.OpenedAt.HasValue)
            {
                share.OpenedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
            }

            return await _pcBuildStorageService.GetBuildDetailByIdAsync(share.PcBuildId);
        }
    }
}
