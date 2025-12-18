using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Eshop.Repository.Components
{
    public class PublishersViewComponent : ViewComponent
    {
        private readonly DataContext _context;
        public PublishersViewComponent(DataContext context)
        {
            _context = context;
        }
        public async Task<IViewComponentResult> InvokeAsync() => View(await _context.Publishers.ToListAsync());

    }
}
