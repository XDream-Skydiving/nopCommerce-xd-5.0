using Microsoft.AspNetCore.Mvc;
using Moq;
using Nop.Core.Domain.Orders;
using Nop.Services.Localization;
using Nop.Web.Areas.Admin.Controllers;
using Nop.Web.Areas.Admin.Factories;
using Nop.Web.Areas.Admin.Models.Orders;
using Nop.Web.Framework.Models.DataTables;
using NUnit.Framework;

namespace Nop.Tests.Nop.Web.Tests.Admin.Factories;

[TestFixture]
public class LatestOrdersTests
{
    [Test]
    public async Task DashboardGridUsesFilteredEndpointAndExpectedColumns()
    {
        var localization = new Mock<ILocalizationService>();
        localization.Setup(service => service.GetResourceAsync(It.IsAny<string>()))
            .ReturnsAsync((string key) => key);
        var factory = new HomeModelFactory(null, null, localization.Object, null, null, null, null, null, null);

        var grid = await factory.PrepareLatestOrdersModelAsync(new DataTablesModel());

        Assert.Multiple(() =>
        {
            Assert.That(grid.UrlRead.ActionName, Is.EqualTo("LatestOrders"));
            Assert.That(grid.UrlRead.ControllerName, Is.EqualTo("Order"));
            Assert.That(grid.Length, Is.EqualTo(5));
            Assert.That(grid.ColumnCollection.Select(column => column.Data), Is.EqualTo(new[]
            {
                nameof(OrderModel.OrderStatus), nameof(OrderModel.CustomerEmail), nameof(OrderModel.CreatedOn),
                nameof(OrderModel.OrderTotal), nameof(OrderModel.Id)
            }));
            Assert.That(grid.ColumnCollection.Last().Render, Is.InstanceOf<RenderCustom>());
            Assert.That(((RenderCustom)grid.ColumnCollection.Last().Render).FunctionName, Is.EqualTo("renderLatestOrderNumber"));
        });
    }

    [Test]
    public async Task LatestOrdersAppliesStatusesBeforePreparingPagedResults()
    {
        OrderSearchModel captured = null;
        var expected = new OrderListModel();
        var factory = new Mock<IOrderModelFactory>();
        factory.Setup(service => service.PrepareOrderListModelAsync(It.IsAny<OrderSearchModel>()))
            .Callback<OrderSearchModel>(model => captured = model)
            .ReturnsAsync(expected);
        var controller = new OrderController(null, null, null, null, null, null, null, null, null, null,
            null, null, factory.Object, null, null, null, null, null, null, null, null, null, null,
            null, null, null, null, null, null, null);
        var search = new OrderSearchModel { Start = 5, Length = 5, OrderStatusIds = [(int)OrderStatus.Complete] };

        var result = await controller.LatestOrders(search);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.InstanceOf<JsonResult>());
            Assert.That(((JsonResult)result).Value, Is.SameAs(expected));
            Assert.That(captured.OrderStatusIds, Is.EqualTo(new[] { (int)OrderStatus.Pending, (int)OrderStatus.Processing }));
            Assert.That(captured.Start, Is.EqualTo(5));
            Assert.That(captured.Length, Is.EqualTo(5));
        });
    }
}
