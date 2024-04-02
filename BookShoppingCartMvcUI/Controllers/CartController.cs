using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using BookShoppingCartMvcUI.Services;

namespace BookShoppingCartMvcUI.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ICartRepository _cartRepo;
        private readonly IConfiguration _config;

        public CartController(IConfiguration config, ICartRepository cartRepo)
        {
            _config = config;
            _cartRepo = cartRepo;
        }

        public async Task<IActionResult> AddItem(int bookId, int qty = 1, int redirect = 0)
        {
            var cartCount = await _cartRepo.AddItem(bookId, qty);
            if (redirect == 0)
                return Ok(cartCount);
            return RedirectToAction("GetUserCart");
        }

        public async Task<IActionResult> RemoveItem(int bookId)
        {
            var cartCount = await _cartRepo.RemoveItem(bookId);
            return RedirectToAction("GetUserCart");
        }

        public async Task<IActionResult> GetUserCart()
        {
            var cart = await _cartRepo.GetUserCart();
            return View(cart);
        }

        public async Task<IActionResult> GetTotalItemInCart()
        {
            int cartItem = await _cartRepo.GetCartItemCount();
            return Ok(cartItem);
        }

        public async Task<IActionResult> Checkout()
        {
            int newOrderId = await _cartRepo.DoCheckout();
            if (newOrderId == 0)
                throw new Exception("Something happened on the server side");

            var userEmail = await _cartRepo.GetUserEmail();
            await Execute(userEmail, newOrderId);
            return RedirectToAction("UserOrders", "UserOrder");
        }

        private string LoadEmailTemplate()
        {
            return @"
            <!DOCTYPE html>
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"">
                <meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
                <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
                <title>Order Confirmation</title>
            </head>
            <body>
                <h2>Order Confirmation</h2>
                <p>Dear Valued Customer,</p>
                <p>We're excited to inform you that your order has been confirmed. Below are the details of your order:</p>
                <table border=""1"">
                    <tr>
                        <th>Order Time</th>
                        <td>{{OrderTime}}</td>
                    </tr>
                    <tr>
                        <th>Book Name</th>
                        <th>Author</th>
                        <th>Quantity</th>
                        <th>Unit Price</th>
                        <th>Total Price</th>
                    </tr>
                    {{OrderItems}}
                    <tr>
                        <th colspan=""4"">Total</th>
                        <td>{{TotalPrice}}</td>
                    </tr>
                </table>
                <p>Thank you for shopping with us!</p>
            </body>
            </html>";
        }

        public async Task Execute(string toEmail, int newOrderId)
        {
            var order = await _cartRepo.GetOrderDetails(newOrderId);

            var orderTimePST = TimeZoneInfo.ConvertTimeFromUtc(order.CreateDate, TimeZoneInfo.FindSystemTimeZoneById("Pacific Standard Time"));
            var orderTime = orderTimePST.ToString("yyyy-MM-dd HH:mm:ss");

            var orderItemsHtml = ""; 
            var totalPrice = 0.0;
            foreach (var detail in order.OrderDetail)
            {
                orderItemsHtml += $@"
                    <tr>
                        <td>{detail.Book.BookName}</td>
                        <td>{detail.Book.AuthorName}</td>
                        <td>{detail.Quantity}</td>
                        <td>{detail.UnitPrice}</td>
                        <td>{detail.Quantity * detail.UnitPrice}</td>
                    </tr>";

                totalPrice += detail.Quantity * detail.UnitPrice;
            }
            var template = LoadEmailTemplate();

            template = template.Replace("{{OrderTime}}", orderTime);
            template = template.Replace("{{OrderItems}}", orderItemsHtml);
            template = template.Replace("{{TotalPrice}}", totalPrice.ToString());

            var _apiKey = _config["AuthMessageSenderOptions:SendGridKey"];
            var apiKey = _apiKey;
            var client = new SendGridClient(apiKey);
            var from = new EmailAddress("jiachzha@gmail.com", "Order Confirmation");
            var subject = $"Your order #{order.Id} has been confirmed";
            var to = new EmailAddress(toEmail);
            var plainTextContent = "";
            var htmlContent = template;
            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainTextContent, htmlContent);
            var response = await client.SendEmailAsync(msg);
        }
    }
}
