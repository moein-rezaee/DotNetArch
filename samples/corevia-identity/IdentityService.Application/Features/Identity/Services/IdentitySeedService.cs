using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using IdentityService.Application.Features.Identity.Options;
using IdentityService.Domain.Entities;
using IdentityService.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace IdentityService.Application.Features.Identity.Services;

public sealed class IdentitySeedService : IIdentitySeedService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IdentityClientOptions _clientOptions;
    private readonly ILogger<IdentitySeedService> _logger;

    public IdentitySeedService(IUnitOfWork unitOfWork, IOptions<IdentityClientOptions> clientOptions, ILogger<IdentitySeedService> logger)
    {
        _unitOfWork = unitOfWork;
        _clientOptions = clientOptions.Value;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("🌱 Starting complete identity seed process...");

        try
        {
            _logger.LogInformation("📋 Step 1/5: Seeding roles...");
            await SeedRolesAsync(cancellationToken);
            _logger.LogInformation("✅ Roles seeded successfully");

            _logger.LogInformation("📋 Step 2/5: Seeding permissions...");
            await SeedPermissionsAsync(cancellationToken);
            _logger.LogInformation("✅ Permissions seeded successfully");

            _logger.LogInformation("📋 Step 3/5: Seeding scopes...");
            await SeedScopesAsync(cancellationToken);
            _logger.LogInformation("✅ Scopes seeded successfully");

            _logger.LogInformation("📋 Step 4/5: Seeding clients...");
            await SeedClientAsync(cancellationToken);
            _logger.LogInformation("✅ Clients seeded successfully");

            _logger.LogInformation("✅ Complete identity seed process finished successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error during identity seed process: {Message}", ex.Message);
            throw;
        }
    }

    private async Task SeedRolesAsync(CancellationToken cancellationToken)
    {
        var roleRepo = _unitOfWork.Repository<Role>();

        var roles = new[]
        {
            new { Name = "SuperAdmin", DisplayName = "Super Admin", Description = "Full access to all services.", IsSystem = true },
            new { Name = "Customer", DisplayName = "Customer", Description = "End-customer using the OnlineShop website.", IsSystem = true }
        };

        foreach (var r in roles)
        {
            var existing = roleRepo.Query().FirstOrDefault(x => x.Name == r.Name);
            if (existing is null)
            {
                await roleRepo.AddAsync(new Role
                {
                    Id = Guid.NewGuid(),
                    Name = r.Name,
                    DisplayName = r.DisplayName,
                    Description = r.Description,
                    CreatedAt = DateTime.UtcNow,
                    IsSystem = r.IsSystem
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedPermissionsAsync(CancellationToken cancellationToken)
    {
        var permissionRepo = _unitOfWork.Repository<Permission>();

        var permissions = new[]
        {
            // Identity Service - Profile
            new { Key = "Identity.Profile.Get", DisplayName = "Read own identity profile", Description = "GET /v1/api/Profile" },
            new { Key = "Identity.Profile.Put", DisplayName = "Update own identity profile", Description = "PUT /v1/api/Profile" },
            new { Key = "Identity.Token.Refresh", DisplayName = "Refresh access token", Description = "POST /v1/api/Identity/Refresh" },
            new { Key = "Identity.Token.Logout", DisplayName = "Logout and revoke refresh token", Description = "POST /v1/api/Identity/Logout" },
            new { Key = "Identity.Mcp.Self.Read", DisplayName = "Read own Identity MCP data", Description = "Identity MCP self read capabilities." },
            new { Key = "Identity.Mcp.Self.Write", DisplayName = "Write own Identity MCP data", Description = "Identity MCP self write capabilities; separately approved when exposed." },
            new { Key = "Identity.Mcp.Admin.Read", DisplayName = "Read tenant-scoped Identity MCP data", Description = "Identity MCP administrative read capabilities." },
            new { Key = "Identity.Mcp.Admin.Write", DisplayName = "Write tenant-scoped Identity MCP data", Description = "Identity MCP administrative write capabilities; exact approval required." },
            new { Key = "Identity.Mcp.Security.Read", DisplayName = "Read Identity MCP security metadata", Description = "Identity MCP security metadata capabilities; disabled by default." },
            new { Key = "Identity.Mcp.Security.Write", DisplayName = "Write Identity MCP security metadata", Description = "Identity MCP security capabilities; disabled by default and separately approved." },

            // Identity API permission catalog (each entry has an explicit MCP counterpart in the manifest).
            new { Key = "Identity.Sessions.Get", DisplayName = "Read own sessions", Description = "GET /v1/api/Sessions" },
            new { Key = "Identity.Sessions.Revoke", DisplayName = "Revoke own session", Description = "DELETE /v1/api/Sessions/{id}" },
            new { Key = "Identity.Sessions.RevokeOthers", DisplayName = "Revoke other own sessions", Description = "DELETE /v1/api/Sessions" },
            new { Key = "Identity.Users.AccessSummary.Get", DisplayName = "Read tenant user access summary", Description = "Identity Application user, role, and tenant access summary." },
            new { Key = "Identity.Sessions.AdminRevoke", DisplayName = "Revoke tenant user sessions", Description = "Identity administrative session revocation." },
            new { Key = "Identity.Users.Read", DisplayName = "Read users", Description = "GET /v1/api/Users and GET /v1/api/Users/{id}" },
            new { Key = "Identity.Users.Write", DisplayName = "Create or update users", Description = "POST/PUT /v1/api/Users" },
            new { Key = "Identity.Users.Delete", DisplayName = "Delete users", Description = "DELETE /v1/api/Users/{id}" },
            new { Key = "Identity.Roles.Read", DisplayName = "Read roles", Description = "GET /v1/api/Roles and GET /v1/api/Roles/{id}" },
            new { Key = "Identity.Roles.Write", DisplayName = "Create or update roles", Description = "POST/PUT /v1/api/Roles" },
            new { Key = "Identity.Roles.Delete", DisplayName = "Delete roles", Description = "DELETE /v1/api/Roles/{id}" },
            new { Key = "Identity.Tenants.Read", DisplayName = "Read tenants", Description = "GET /v1/api/Tenants and GET /v1/api/Tenants/{id}" },
            new { Key = "Identity.Tenants.Write", DisplayName = "Create or update tenants", Description = "POST/PUT /v1/api/Tenants" },
            new { Key = "Identity.Tenants.Delete", DisplayName = "Delete tenants", Description = "DELETE /v1/api/Tenants/{id}" },
            new { Key = "Identity.Permissions.Read", DisplayName = "Read permissions", Description = "GET /v1/api/Permissions and GET /v1/api/Permissions/{id}" },
            new { Key = "Identity.Permissions.Write", DisplayName = "Create or update permissions", Description = "POST/PUT /v1/api/Permissions" },
            new { Key = "Identity.Permissions.Delete", DisplayName = "Delete permissions", Description = "DELETE /v1/api/Permissions/{id}" },
            new { Key = "Identity.Scopes.Read", DisplayName = "Read scopes", Description = "GET /v1/api/Scopes and GET /v1/api/Scopes/{id}" },
            new { Key = "Identity.Scopes.Write", DisplayName = "Create or update scopes", Description = "POST/PUT /v1/api/Scopes" },
            new { Key = "Identity.Scopes.Delete", DisplayName = "Delete scopes", Description = "DELETE /v1/api/Scopes/{id}" },
            new { Key = "Identity.Clients.Read", DisplayName = "Read clients", Description = "GET /v1/api/Clients and GET /v1/api/Clients/{id}" },
            new { Key = "Identity.Clients.Write", DisplayName = "Create or update clients", Description = "POST/PUT /v1/api/Clients" },
            new { Key = "Identity.UserRoles.Read", DisplayName = "Read user roles", Description = "GET /v1/api/Users/{userId}/Roles" },
            new { Key = "Identity.UserRoles.Write", DisplayName = "Change user roles", Description = "POST/DELETE /v1/api/Users/{userId}/Roles" },
            new { Key = "Identity.UserTenants.Read", DisplayName = "Read user tenants", Description = "GET /v1/api/Users/{userId}/Tenants" },
            new { Key = "Identity.UserTenants.Write", DisplayName = "Change user tenants", Description = "POST/DELETE /v1/api/Users/{userId}/Tenants" },
            new { Key = "Identity.RolePermissions.Read", DisplayName = "Read role permissions", Description = "GET /v1/api/Roles/{roleId}/Permissions" },
            new { Key = "Identity.RolePermissions.Write", DisplayName = "Change role permissions", Description = "POST/DELETE /v1/api/Roles/{roleId}/Permissions" },
            new { Key = "Identity.ScopePermissions.Read", DisplayName = "Read scope permissions", Description = "GET /v1/api/Scopes/{scopeId}/Permissions" },
            new { Key = "Identity.ScopePermissions.Write", DisplayName = "Change scope permissions", Description = "POST/DELETE /v1/api/Scopes/{scopeId}/Permissions" },
            new { Key = "Identity.ClientScopes.Read", DisplayName = "Read client scopes", Description = "GET /v1/api/Clients/{clientId}/Scopes" },
            new { Key = "Identity.ClientScopes.Write", DisplayName = "Change client scopes", Description = "POST/DELETE /v1/api/Clients/{clientId}/Scopes" },
            new { Key = "Identity.RoleUsers.Read", DisplayName = "Read role users", Description = "GET /v1/api/Roles/{roleId}/Users" },
            new { Key = "Identity.TenantUsers.Read", DisplayName = "Read tenant users", Description = "GET /v1/api/Tenants/{tenantId}/Users" },

            // MCP capability permissions. These are deliberately separate grants from API permissions.
            new { Key = "Identity.Mcp.Profile.Read", DisplayName = "MCP profile read", Description = "Explicit MCP grant for identity profile read." },
            new { Key = "Identity.Mcp.Profile.Write", DisplayName = "MCP profile write", Description = "Explicit MCP grant for identity profile write." },
            new { Key = "Identity.Mcp.Sessions.Read", DisplayName = "MCP sessions read", Description = "Explicit MCP grant for self session read." },
            new { Key = "Identity.Mcp.SessionsRevoke.Sensitive", DisplayName = "MCP self session revoke", Description = "Explicit sensitive MCP grant for self session revocation." },
            new { Key = "Identity.Mcp.SessionsRevokeOthers.Sensitive", DisplayName = "MCP other sessions revoke", Description = "Explicit sensitive MCP grant for revoking other self sessions." },
            new { Key = "Identity.Mcp.UserAccessSummary.Read", DisplayName = "MCP user access summary read", Description = "Explicit MCP grant for tenant-authorized access summaries." },
            new { Key = "Identity.Mcp.SessionsAdmin.Sensitive", DisplayName = "MCP administrative session revoke", Description = "Explicit sensitive MCP grant for administrative session revocation." },
            new { Key = "Identity.Mcp.Users.Read", DisplayName = "MCP users read", Description = "Explicit MCP grant for user read." },
            new { Key = "Identity.Mcp.Users.Write", DisplayName = "MCP users write", Description = "Explicit MCP grant for user write." },
            new { Key = "Identity.Mcp.Users.Sensitive", DisplayName = "MCP users sensitive", Description = "Explicit sensitive MCP grant for user deletion." },
            new { Key = "Identity.Mcp.Roles.Read", DisplayName = "MCP roles read", Description = "Explicit MCP grant for role read." },
            new { Key = "Identity.Mcp.Roles.Write", DisplayName = "MCP roles write", Description = "Explicit MCP grant for role write." },
            new { Key = "Identity.Mcp.Roles.Sensitive", DisplayName = "MCP roles sensitive", Description = "Explicit sensitive MCP grant for role deletion." },
            new { Key = "Identity.Mcp.Tenants.Read", DisplayName = "MCP tenants read", Description = "Explicit MCP grant for tenant read." },
            new { Key = "Identity.Mcp.Tenants.Write", DisplayName = "MCP tenants write", Description = "Explicit MCP grant for tenant write." },
            new { Key = "Identity.Mcp.Tenants.Sensitive", DisplayName = "MCP tenants sensitive", Description = "Explicit sensitive MCP grant for tenant deletion." },
            new { Key = "Identity.Mcp.Permissions.Read", DisplayName = "MCP permissions read", Description = "Explicit MCP grant for permission read." },
            new { Key = "Identity.Mcp.Permissions.Write", DisplayName = "MCP permissions write", Description = "Explicit MCP grant for permission write." },
            new { Key = "Identity.Mcp.Permissions.Sensitive", DisplayName = "MCP permissions sensitive", Description = "Explicit sensitive MCP grant for permission deletion." },
            new { Key = "Identity.Mcp.Scopes.Read", DisplayName = "MCP scopes read", Description = "Explicit MCP grant for scope read." },
            new { Key = "Identity.Mcp.Scopes.Write", DisplayName = "MCP scopes write", Description = "Explicit MCP grant for scope write." },
            new { Key = "Identity.Mcp.Scopes.Sensitive", DisplayName = "MCP scopes sensitive", Description = "Explicit sensitive MCP grant for scope deletion." },
            new { Key = "Identity.Mcp.Clients.Read", DisplayName = "MCP clients read", Description = "Explicit MCP grant for client read." },
            new { Key = "Identity.Mcp.Clients.Write", DisplayName = "MCP clients write", Description = "Explicit MCP grant for client write." },
            new { Key = "Identity.Mcp.UserRoles.Read", DisplayName = "MCP user roles read", Description = "Explicit MCP grant for user role read." },
            new { Key = "Identity.Mcp.UserRoles.Sensitive", DisplayName = "MCP user roles sensitive", Description = "Explicit sensitive MCP grant for user role changes." },
            new { Key = "Identity.Mcp.UserTenants.Read", DisplayName = "MCP user tenants read", Description = "Explicit MCP grant for user tenant read." },
            new { Key = "Identity.Mcp.UserTenants.Sensitive", DisplayName = "MCP user tenants sensitive", Description = "Explicit sensitive MCP grant for user tenant changes." },
            new { Key = "Identity.Mcp.RolePermissions.Read", DisplayName = "MCP role permissions read", Description = "Explicit MCP grant for role permission read." },
            new { Key = "Identity.Mcp.RolePermissions.Sensitive", DisplayName = "MCP role permissions sensitive", Description = "Explicit sensitive MCP grant for role permission changes." },
            new { Key = "Identity.Mcp.ScopePermissions.Read", DisplayName = "MCP scope permissions read", Description = "Explicit MCP grant for scope permission read." },
            new { Key = "Identity.Mcp.ScopePermissions.Sensitive", DisplayName = "MCP scope permissions sensitive", Description = "Explicit sensitive MCP grant for scope permission changes." },
            new { Key = "Identity.Mcp.ClientScopes.Read", DisplayName = "MCP client scopes read", Description = "Explicit MCP grant for client scope read." },
            new { Key = "Identity.Mcp.ClientScopes.Sensitive", DisplayName = "MCP client scopes sensitive", Description = "Explicit sensitive MCP grant for client scope changes." },
            new { Key = "Identity.Mcp.RoleUsers.Read", DisplayName = "MCP role users read", Description = "Explicit MCP grant for role user read." },
            new { Key = "Identity.Mcp.TenantUsers.Read", DisplayName = "MCP tenant users read", Description = "Explicit MCP grant for tenant user read." },
            new { Key = "Identity.Mcp.Protocol.Refresh", DisplayName = "MCP protocol refresh", Description = "Deprecated non-exposable protocol binding; raw tokens are forbidden." },
            new { Key = "Identity.Mcp.Protocol.Logout", DisplayName = "MCP protocol logout", Description = "Deprecated non-exposable protocol binding; raw tokens are forbidden." },
            new { Key = "Identity.Mcp.Protocol.ClientSecrets.Read", DisplayName = "MCP protocol client secret read", Description = "Deprecated non-exposable protocol binding; raw secrets are forbidden." },
            new { Key = "Identity.Mcp.Protocol.ClientSecrets.Write", DisplayName = "MCP protocol client secret write", Description = "Deprecated non-exposable protocol binding; raw secrets are forbidden." },
            new { Key = "Identity.Mcp.Protocol.ClientSecrets.Revoke", DisplayName = "MCP protocol client secret revoke", Description = "Deprecated non-exposable protocol binding; raw secrets are forbidden." },

            // Customer Service - Me endpoints
            new { Key = "Customer.Me.Customer.Get", DisplayName = "Read current customer profile", Description = "GET /v1/api/Me/Customer" },
            new { Key = "Customer.Me.Customer.Post", DisplayName = "Create current customer profile", Description = "POST /v1/api/Me/Customer" },
            new { Key = "Customer.Me.Customer.Put", DisplayName = "Update current customer profile", Description = "PUT /v1/api/Me/Customer" },
            new { Key = "Customer.Me.Addresses.Get", DisplayName = "Read own addresses", Description = "GET /v1/api/Me/Addresses" },
            new { Key = "Customer.Me.Addresses.Post", DisplayName = "Create own address", Description = "POST /v1/api/Me/Addresses" },
            new { Key = "Customer.Me.Addresses.Put", DisplayName = "Update own address", Description = "PUT /v1/api/Me/Addresses/{id}" },
            new { Key = "Customer.Me.Addresses.Delete", DisplayName = "Delete own address", Description = "DELETE /v1/api/Me/Addresses/{id}" },

            // Customer Service - Admin endpoints
            new { Key = "Customer.Admin.Get", DisplayName = "Read any customer", Description = "GET /v1/api/Customer/{id}" },
            new { Key = "Customer.Admin.List", DisplayName = "List all customers", Description = "GET /v1/api/Customer/Paged" },
            new { Key = "Customer.Category.Manage", DisplayName = "Manage customer categories", Description = "POST/PUT/DELETE /v1/api/CustomerCategory" },
            new { Key = "Customer.Address.Manage", DisplayName = "Manage customer addresses", Description = "POST/PUT/DELETE /v1/api/Address" },
            new { Key = "Customer.Phone.Manage", DisplayName = "Manage customer phones", Description = "POST/PUT/DELETE /v1/api/Phone" },
            new { Key = "Customer.RealCustomer.Manage", DisplayName = "Manage real customers", Description = "POST/PUT/DELETE /v1/api/RealCustomer" },
            new { Key = "Customer.LegalCustomer.Manage", DisplayName = "Manage legal customers", Description = "POST/PUT/DELETE /v1/api/LegalCustomer" },

            // Basket Service - Me endpoints (Customer accessible)
            new { Key = "Basket.Me.Get", DisplayName = "Read own basket", Description = "GET /v1/api/Me/Basket" },
            new { Key = "Basket.Me.Post", DisplayName = "Add item to own basket", Description = "POST /v1/api/Me/Basket" },
            new { Key = "Basket.Me.Put", DisplayName = "Update own basket item", Description = "PUT /v1/api/Me/Basket/{id}" },
            new { Key = "Basket.Me.Delete", DisplayName = "Remove item from own basket", Description = "DELETE /v1/api/Me/Basket/{id}" },
            new { Key = "Basket.Me.Clear", DisplayName = "Clear own basket", Description = "DELETE /v1/api/Me/Basket" },
            new { Key = "Basket.Me.Checkout", DisplayName = "Checkout own basket", Description = "POST /v1/api/Me/Basket/Checkout" },

            // Basket Service - Admin endpoints
            new { Key = "Basket.Admin.Get", DisplayName = "Read any user basket", Description = "GET /v1/api/Basket/{userId}" },
            new { Key = "Basket.Admin.List", DisplayName = "List all baskets", Description = "GET /v1/api/Basket" },

            // Order Service - Me endpoints (Customer accessible)
            new { Key = "Order.Me.Get", DisplayName = "Read own orders", Description = "GET /v1/api/Me/Orders" },
            new { Key = "Order.Me.GetById", DisplayName = "Read own order by ID", Description = "GET /v1/api/Me/Orders/{id}" },

            // Order Service - Admin endpoints
            new { Key = "Order.Admin.Get", DisplayName = "Read any order", Description = "GET /v1/api/Order/{id}" },
            new { Key = "Order.Admin.List", DisplayName = "List all orders", Description = "GET /v1/api/Order" },
            new { Key = "Order.Admin.Post", DisplayName = "Create order", Description = "POST /v1/api/Order" },
            new { Key = "Order.Admin.Put", DisplayName = "Update order", Description = "PUT /v1/api/Order/{id}" },
            new { Key = "Order.Admin.Delete", DisplayName = "Delete order", Description = "DELETE /v1/api/Order/{id}" },

            // Invoice Service - Me endpoints (Customer accessible)
            new { Key = "Invoice.Me.Get", DisplayName = "Read own invoices", Description = "GET /v1/api/Me/Invoices" },
            new { Key = "Invoice.Me.GetById", DisplayName = "Read own invoice by ID", Description = "GET /v1/api/Me/Invoices/{id}" },

            // Invoice Service - Admin endpoints
            new { Key = "Invoice.Admin.Get", DisplayName = "Read any invoice", Description = "GET /v1/api/Invoice/{id}" },
            new { Key = "Invoice.Admin.List", DisplayName = "List all invoices", Description = "GET /v1/api/Invoice/Paged" },
            new { Key = "Invoice.Admin.Post", DisplayName = "Create invoice", Description = "POST /v1/api/Invoice" },
            new { Key = "Invoice.Admin.Batch", DisplayName = "Batch create invoices", Description = "POST /v1/api/Invoice/Batch" },
            new { Key = "Invoice.Admin.FromQuotation", DisplayName = "Create invoice from quotation", Description = "POST /v1/api/Invoice/BasedOnQuotation" },

            // Receipt Service - Admin endpoints
            new { Key = "Receipt.Admin.Post", DisplayName = "Create receipt", Description = "POST /v1/api/Receipt/BasedOnInvoice" },

            // Payment Service - Me endpoints (Customer accessible)
            new { Key = "Payment.Me.Get", DisplayName = "Read own payments", Description = "GET /v1/api/Me/Payments" },
            new { Key = "Payment.Me.GetById", DisplayName = "Read own payment by ID", Description = "GET /v1/api/Me/Payments/{id}" },

            // Payment Service - Initiation (Customer accessible)
            new { Key = "Payment.Start", DisplayName = "Start payment", Description = "POST /v1/api/Payment" },
            new { Key = "Payment.Verify", DisplayName = "Verify payment", Description = "POST /v1/api/Payment/Verify" },

            // Payment Service - Admin endpoints
            new { Key = "Payment.Admin.Get", DisplayName = "Read any payment", Description = "GET /v1/api/Payment/{id}" },
            new { Key = "Payment.Admin.List", DisplayName = "List all payments", Description = "GET /v1/api/Payment/Paged" },
            new { Key = "Payment.Admin.Refund", DisplayName = "Refund payment", Description = "POST /v1/api/Payment/Refund" },

            // Catalog Service - Admin endpoints (Read-only for catalog is public)
            new { Key = "Catalog.Product.ByCode.Get", DisplayName = "Read product by code", Description = "GET /v1/api/Product/ByCode/{code}" },
            new { Key = "Catalog.Product.WithoutImageExcel.Get", DisplayName = "Export products without image", Description = "GET /v1/api/Product/WithoutImageExcel" },
            new { Key = "Catalog.Unit.Manage", DisplayName = "Manage units", Description = "POST/PUT/DELETE /v1/api/Unit" },
            new { Key = "Catalog.Currency.Manage", DisplayName = "Manage currencies", Description = "POST/PUT/DELETE /v1/api/Currency" },
            new { Key = "Catalog.Stock.Manage", DisplayName = "Manage stocks", Description = "POST/PUT/DELETE /v1/api/Stock" },
            new { Key = "Catalog.SaleType.Manage", DisplayName = "Manage sale types", Description = "POST/PUT/DELETE /v1/api/SaleType" },
            new { Key = "Catalog.Item.Manage", DisplayName = "Manage items", Description = "POST/PUT/DELETE /v1/api/Item" },
            new { Key = "Catalog.Price.Manage", DisplayName = "Manage prices", Description = "POST/PUT/DELETE /v1/api/Price" },

            // DidarSync Service - Operational endpoints
            new { Key = "DidarSync.Outbox.Get", DisplayName = "Read DidarSync outbox", Description = "GET /v1/api/outbox" },
            new { Key = "DidarSync.Outbox.Retry", DisplayName = "Retry DidarSync outbox item", Description = "POST /v1/api/outbox/{id}/retry" },
            new { Key = "DidarSync.Products.Sync", DisplayName = "Run Didar product sync", Description = "POST /v1/api/products/sync/*" },
            new { Key = "DidarSync.Orders.Sync", DisplayName = "Run Didar order sync", Description = "POST /v1/api/orders/sync/*" },

            // M2M (Machine-to-Machine) Permissions
            new { Key = "Otp.Send", DisplayName = "Send OTP code", Description = "POST /v1/api/Otp/Send (M2M)" },
            new { Key = "Otp.Verify", DisplayName = "Verify OTP code", Description = "POST /v1/api/Otp/Verify (M2M)" },
            new { Key = "Notification.Send", DisplayName = "Send notification", Description = "POST /v1/api/Notification/Send (M2M)" },
            new { Key = "Customer.ByPhone.Get", DisplayName = "Get customer by phone", Description = "GET /v1/api/Customer/ByPhone/{phone} (M2M)" },
            
            // Domain-specific Sepidar Permissions (replaces generic Sepidar.Read/Write)
            new { Key = "Sepidar.Customers.Read", DisplayName = "Read Sepidar Customers", Description = "GET /v1/api/Customers, CustomerGroupings (M2M - CustomerService only)" },
            new { Key = "Sepidar.Customers.Write", DisplayName = "Write Sepidar Customers", Description = "POST/PUT /v1/api/Customers (M2M - CustomerService only)" },
            new { Key = "Sepidar.Catalog.Read", DisplayName = "Read Sepidar Catalog", Description = "GET /v1/api/Items, Units, Currencies, Stocks, SaleTypes, Properties, PriceNoteItems (M2M - CatalogService only)" },
            new { Key = "Sepidar.Orders.Read", DisplayName = "Read Sepidar Orders", Description = "GET /v1/api/Quotations (M2M - OrderService only)" },
            new { Key = "Sepidar.Orders.Write", DisplayName = "Write Sepidar Orders", Description = "POST/DELETE /v1/api/Quotations, Close/UnClose (M2M - OrderService only)" },
            new { Key = "Sepidar.Invoices.Read", DisplayName = "Read Sepidar Invoices", Description = "GET /v1/api/Invoices (M2M - InvoiceService only)" },
            new { Key = "Sepidar.Invoices.Write", DisplayName = "Write Sepidar Invoices", Description = "POST /v1/api/Invoices, Receipts/BasedOnInvoice (M2M - InvoiceService only)" },
            new { Key = "Sepidar.Common.Read", DisplayName = "Read Sepidar Common", Description = "GET /v1/api/AdministrativeDivisions, Banks, BankAccounts, General (M2M - shared)" },
            
            // Service-to-Service domain permissions
            new { Key = "Orders.Read", DisplayName = "Read Orders", Description = "GET /v1/api/Order/* (M2M - InvoiceService reading from OrderService)" },
            new { Key = "Orders.Status.Update", DisplayName = "Update Order Status", Description = "PUT /v1/api/Order/{id}/Status (M2M - PaymentService updating OrderService)" },
            new { Key = "Invoices.Create", DisplayName = "Create Invoices", Description = "POST /v1/api/Invoice/* (M2M - PaymentService calling InvoiceService)" },
            new { Key = "Invoice.Read", DisplayName = "Read invoices", Description = "GET /v1/api/Invoice/* (M2M)" },
            new { Key = "Catalog.Validate", DisplayName = "Validate Catalog Items", Description = "GET /v1/api/Item/*, Price/* (M2M - BasketService validating CatalogService)" },
            
            // Legacy permissions (deprecated)
            new { Key = "Sepidar.Read", DisplayName = "Read from Sepidar (Legacy)", Description = "[DEPRECATED] Use domain-specific permissions" },
            new { Key = "Sepidar.Write", DisplayName = "Write to Sepidar (Legacy)", Description = "[DEPRECATED] Use domain-specific permissions" }
        };

        foreach (var p in permissions)
        {
            var existing = permissionRepo.Query().FirstOrDefault(x => x.Key == p.Key);
            if (existing is null)
            {
                await permissionRepo.AddAsync(new Permission
                {
                    Id = Guid.NewGuid(),
                    Key = p.Key,
                    DisplayName = p.DisplayName,
                    Description = p.Description,
                    CreatedAt = DateTime.UtcNow,
                    IsDeprecated = p.Key.StartsWith("Identity.Token.", StringComparison.Ordinal) ||
                                   p.Key.StartsWith("Identity.ClientSecrets.", StringComparison.Ordinal)
                }, cancellationToken);
            }
            else if (p.Key.StartsWith("Identity.Token.", StringComparison.Ordinal) ||
                     p.Key.StartsWith("Identity.ClientSecrets.", StringComparison.Ordinal))
            {
                existing.IsDeprecated = true;
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await SeedRolePermissionsAsync(cancellationToken);
    }

    private async Task SeedRolePermissionsAsync(CancellationToken cancellationToken)
    {
        var roleRepo = _unitOfWork.Repository<Role>();
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var rolePermissionRepo = _unitOfWork.Repository<RolePermission>();

        var roles = roleRepo.Query().ToDictionary(r => r.Name, r => r.Id);
        var permissions = permissionRepo.Query().ToDictionary(p => p.Key, p => p.Id);

        if (!roles.TryGetValue("SuperAdmin", out var superAdminId))
        {
            return;
        }

        if (!roles.TryGetValue("Customer", out var customerId))
        {
            return;
        }

        var allPermissionIds = permissions.Values.ToList();

        // SuperAdmin gets all permissions
        foreach (var permissionId in allPermissionIds)
        {
            var exists = rolePermissionRepo.Query()
                .Any(rp => rp.RoleId == superAdminId && rp.PermissionId == permissionId);
            if (!exists)
            {
                await rolePermissionRepo.AddAsync(new RolePermission
                {
                    RoleId = superAdminId,
                    PermissionId = permissionId
                }, cancellationToken);
            }
        }

        // Customer gets only customer/self + basket/order/invoice/payment self + identity profile/tokens
        var customerPermissionKeys = new[]
        {
            // Identity
            "Identity.Profile.Get",
            "Identity.Profile.Put",
            "Identity.Sessions.Get",
            "Identity.Token.Refresh",
            "Identity.Token.Logout",
            "Identity.Mcp.Self.Read",
            "Identity.Mcp.Profile.Read",
            "Identity.Mcp.Sessions.Read",

            // Customer - Me
            "Customer.Me.Customer.Get",
            "Customer.Me.Customer.Post",
            "Customer.Me.Customer.Put",
            "Customer.Me.Addresses.Get",
            "Customer.Me.Addresses.Post",
            "Customer.Me.Addresses.Put",
            "Customer.Me.Addresses.Delete",

            // Basket - Me
            "Basket.Me.Get",
            "Basket.Me.Post",
            "Basket.Me.Put",
            "Basket.Me.Delete",
            "Basket.Me.Clear",
            "Basket.Me.Checkout",

            // Order - Me (read-only)
            "Order.Me.Get",
            "Order.Me.GetById",

            // Invoice - Me (read-only)
            "Invoice.Me.Get",
            "Invoice.Me.GetById",

            // Payment - Me + Initiation
            "Payment.Me.Get",
            "Payment.Me.GetById",
            "Payment.Start",

            // Catalog - Read
            "Catalog.Product.ByCode.Get",
            "Catalog.Product.WithoutImageExcel.Get"
        };

        foreach (var key in customerPermissionKeys)
        {
            if (!permissions.TryGetValue(key, out var pid))
            {
                continue;
            }

            var exists = rolePermissionRepo.Query()
                .Any(rp => rp.RoleId == customerId && rp.PermissionId == pid);
            if (!exists)
            {
                await rolePermissionRepo.AddAsync(new RolePermission
                {
                    RoleId = customerId,
                    PermissionId = pid
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedScopesAsync(CancellationToken cancellationToken)
    {
        var scopeRepo = _unitOfWork.Repository<Scope>();

        var scopes = new[]
        {
            // User-facing scopes (for public client / end-users)
            new { Name = "identity.self", DisplayName = "Identity self operations", Description = "Profile and token operations for the current user." },
            new { Name = "identity.mcp.self.read", DisplayName = "Identity MCP self read", Description = "Read-only Identity MCP capabilities for the current user." },
            new { Name = "identity.mcp.self.write", DisplayName = "Identity MCP self write", Description = "Identity MCP self write capabilities; separately approved when exposed." },
            new { Name = "identity.mcp.admin.read", DisplayName = "Identity MCP admin read", Description = "Tenant-scoped Identity MCP administrative read capabilities." },
            new { Name = "identity.mcp.admin.write", DisplayName = "Identity MCP admin write", Description = "Tenant-scoped Identity MCP administrative write capabilities; exact approval required." },
            new { Name = "identity.mcp.security.read", DisplayName = "Identity MCP security read", Description = "Identity MCP security metadata capabilities; disabled by default." },
            new { Name = "identity.mcp.security.write", DisplayName = "Identity MCP security write", Description = "Identity MCP security capabilities; disabled by default and separately approved." },
            // Capability-level MCP scopes. They are explicit grants and never inferred from API scopes.
            new { Name = "identity.mcp.profile.read", DisplayName = "Identity MCP profile read", Description = "Explicit MCP grant for reading the current user's profile." },
            new { Name = "identity.mcp.profile.write", DisplayName = "Identity MCP profile write", Description = "Explicit MCP grant for updating the current user's profile." },
            new { Name = "identity.mcp.sessions.read", DisplayName = "Identity MCP sessions read", Description = "Explicit MCP grant for reading the current user's sessions." },
            new { Name = "identity.mcp.sessions-revoke.sensitive", DisplayName = "Identity MCP session revoke", Description = "Explicit sensitive MCP grant for revoking one own session." },
            new { Name = "identity.mcp.sessions-revoke-others.sensitive", DisplayName = "Identity MCP other sessions revoke", Description = "Explicit sensitive MCP grant for revoking other own sessions." },
            new { Name = "identity.mcp.user-access-summary.read", DisplayName = "Identity MCP user access summary read", Description = "Explicit MCP grant for tenant-authorized access summaries." },
            new { Name = "identity.mcp.sessions-admin.sensitive", DisplayName = "Identity MCP administrative session revoke", Description = "Explicit sensitive MCP grant for administrative session revocation." },
            new { Name = "identity.mcp.users.read", DisplayName = "Identity MCP users read", Description = "Explicit MCP grant for reading users." },
            new { Name = "identity.mcp.users.write", DisplayName = "Identity MCP users write", Description = "Explicit MCP grant for creating or updating users." },
            new { Name = "identity.mcp.users.sensitive", DisplayName = "Identity MCP users sensitive", Description = "Explicit sensitive MCP grant for deleting users." },
            new { Name = "identity.mcp.roles.read", DisplayName = "Identity MCP roles read", Description = "Explicit MCP grant for reading roles." },
            new { Name = "identity.mcp.roles.write", DisplayName = "Identity MCP roles write", Description = "Explicit MCP grant for creating or updating roles." },
            new { Name = "identity.mcp.roles.sensitive", DisplayName = "Identity MCP roles sensitive", Description = "Explicit sensitive MCP grant for deleting roles." },
            new { Name = "identity.mcp.tenants.read", DisplayName = "Identity MCP tenants read", Description = "Explicit MCP grant for reading tenants." },
            new { Name = "identity.mcp.tenants.write", DisplayName = "Identity MCP tenants write", Description = "Explicit MCP grant for creating or updating tenants." },
            new { Name = "identity.mcp.tenants.sensitive", DisplayName = "Identity MCP tenants sensitive", Description = "Explicit sensitive MCP grant for deleting tenants." },
            new { Name = "identity.mcp.permissions.read", DisplayName = "Identity MCP permissions read", Description = "Explicit MCP grant for reading permissions." },
            new { Name = "identity.mcp.permissions.write", DisplayName = "Identity MCP permissions write", Description = "Explicit MCP grant for creating or updating permissions." },
            new { Name = "identity.mcp.permissions.sensitive", DisplayName = "Identity MCP permissions sensitive", Description = "Explicit sensitive MCP grant for deleting permissions." },
            new { Name = "identity.mcp.scopes.read", DisplayName = "Identity MCP scopes read", Description = "Explicit MCP grant for reading scopes." },
            new { Name = "identity.mcp.scopes.write", DisplayName = "Identity MCP scopes write", Description = "Explicit MCP grant for creating or updating scopes." },
            new { Name = "identity.mcp.scopes.sensitive", DisplayName = "Identity MCP scopes sensitive", Description = "Explicit sensitive MCP grant for deleting scopes." },
            new { Name = "identity.mcp.clients.read", DisplayName = "Identity MCP clients read", Description = "Explicit MCP grant for reading clients." },
            new { Name = "identity.mcp.clients.write", DisplayName = "Identity MCP clients write", Description = "Explicit MCP grant for creating or updating clients." },
            new { Name = "identity.mcp.user-roles.read", DisplayName = "Identity MCP user roles read", Description = "Explicit MCP grant for reading user roles." },
            new { Name = "identity.mcp.user-roles.sensitive", DisplayName = "Identity MCP user roles sensitive", Description = "Explicit sensitive MCP grant for changing user roles." },
            new { Name = "identity.mcp.user-tenants.read", DisplayName = "Identity MCP user tenants read", Description = "Explicit MCP grant for reading user tenant memberships." },
            new { Name = "identity.mcp.user-tenants.sensitive", DisplayName = "Identity MCP user tenants sensitive", Description = "Explicit sensitive MCP grant for changing user tenant memberships." },
            new { Name = "identity.mcp.role-permissions.read", DisplayName = "Identity MCP role permissions read", Description = "Explicit MCP grant for reading role permissions." },
            new { Name = "identity.mcp.role-permissions.sensitive", DisplayName = "Identity MCP role permissions sensitive", Description = "Explicit sensitive MCP grant for changing role permissions." },
            new { Name = "identity.mcp.scope-permissions.read", DisplayName = "Identity MCP scope permissions read", Description = "Explicit MCP grant for reading scope permissions." },
            new { Name = "identity.mcp.scope-permissions.sensitive", DisplayName = "Identity MCP scope permissions sensitive", Description = "Explicit sensitive MCP grant for changing scope permissions." },
            new { Name = "identity.mcp.client-scopes.read", DisplayName = "Identity MCP client scopes read", Description = "Explicit MCP grant for reading client scopes." },
            new { Name = "identity.mcp.client-scopes.sensitive", DisplayName = "Identity MCP client scopes sensitive", Description = "Explicit sensitive MCP grant for changing client scopes." },
            new { Name = "identity.mcp.role-users.read", DisplayName = "Identity MCP role users read", Description = "Explicit MCP grant for reading users assigned to a role." },
            new { Name = "identity.mcp.tenant-users.read", DisplayName = "Identity MCP tenant users read", Description = "Explicit MCP grant for reading users assigned to the authenticated tenant." },
            new { Name = "identity.mcp.protocol.refresh", DisplayName = "Identity MCP protocol refresh", Description = "Deprecated protocol grant; never exposed as an MCP Tool." },
            new { Name = "identity.mcp.protocol.logout", DisplayName = "Identity MCP protocol logout", Description = "Deprecated protocol grant; never exposed as an MCP Tool." },
            new { Name = "identity.mcp.protocol.client-secrets.read", DisplayName = "Identity MCP protocol client secret read", Description = "Deprecated protocol grant; raw secrets are never exposed." },
            new { Name = "identity.mcp.protocol.client-secrets.write", DisplayName = "Identity MCP protocol client secret write", Description = "Deprecated protocol grant; raw secrets are never exposed." },
            new { Name = "identity.mcp.protocol.client-secrets.revoke", DisplayName = "Identity MCP protocol client secret revoke", Description = "Deprecated protocol grant; raw secrets are never exposed." },
            new { Name = "customer.self", DisplayName = "Customer self operations", Description = "Customer profile, addresses, and phones for the current user." },
            new { Name = "catalog.read", DisplayName = "Catalog read", Description = "Read-only access to catalog products." },
            new { Name = "basket.self", DisplayName = "Basket self operations", Description = "Basket management for the current user." },
            new { Name = "order.self", DisplayName = "Order self operations", Description = "Read own orders." },
            new { Name = "invoice.self", DisplayName = "Invoice self operations", Description = "Read own invoices." },
            new { Name = "payment.self", DisplayName = "Payment self operations", Description = "Payment initiation and read own payments." },
            
            // M2M scopes - Internal services communication
            new { Name = "otp.send", DisplayName = "Send OTP", Description = "Machine-to-machine scope for sending OTP codes." },
            new { Name = "notification.send", DisplayName = "Send Notification", Description = "Machine-to-machine scope for sending SMS/email notifications." },
            new { Name = "customer.read", DisplayName = "Read Customer", Description = "Machine-to-machine scope for reading customer data by phone." },
            new { Name = "customer.write", DisplayName = "Write Customer", Description = "Machine-to-machine scope for creating/updating customer data." },
            
            // M2M scopes - Domain-specific SepidarGateway access (Scope Isolation)
            new { Name = "sepidar.customers.read", DisplayName = "Read Sepidar Customers", Description = "M2M scope for reading Customers/CustomerGroupings from SepidarGateway (CustomerService only)." },
            new { Name = "sepidar.customers.write", DisplayName = "Write Sepidar Customers", Description = "M2M scope for creating/updating Customers in SepidarGateway (CustomerService only)." },
            new { Name = "sepidar.catalog.read", DisplayName = "Read Sepidar Catalog", Description = "M2M scope for reading Items/Units/Currencies/Stocks from SepidarGateway (CatalogService only)." },
            new { Name = "sepidar.orders.read", DisplayName = "Read Sepidar Orders", Description = "M2M scope for reading Quotations from SepidarGateway (OrderService only)." },
            new { Name = "sepidar.orders.write", DisplayName = "Write Sepidar Orders", Description = "M2M scope for creating/updating Quotations in SepidarGateway (OrderService only)." },
            new { Name = "sepidar.invoices.read", DisplayName = "Read Sepidar Invoices", Description = "M2M scope for reading Invoices from SepidarGateway (InvoiceService only)." },
            new { Name = "sepidar.invoices.write", DisplayName = "Write Sepidar Invoices", Description = "M2M scope for creating Invoices/Receipts in SepidarGateway (InvoiceService only)." },
            new { Name = "sepidar.common.read", DisplayName = "Read Sepidar Common", Description = "M2M scope for reading common data (Banks/BankAccounts/AdministrativeDivisions) from SepidarGateway." },
            
            // M2M scopes - Service-to-Service domain access
            new { Name = "orders.read", DisplayName = "Read Orders", Description = "M2M scope for InvoiceService to read order data from OrderService." },
            new { Name = "orders.status", DisplayName = "Update Order Status", Description = "M2M scope for PaymentService to update order status in OrderService." },
            new { Name = "order.write", DisplayName = "Write Orders", Description = "M2M scope for BasketService to create orders in OrderService." },
            new { Name = "invoices.create", DisplayName = "Create Invoices", Description = "M2M scope for PaymentService to create invoices in InvoiceService." },
            new { Name = "invoices.read", DisplayName = "Read Invoices", Description = "M2M scope for PaymentService to read invoices from InvoiceService." },
            new { Name = "pos.payment.start", DisplayName = "Start POS Payment", Description = "M2M scope for InvoiceService to start an invoice-bound POS payment." },
            new { Name = "invoice.payment.confirm", DisplayName = "Confirm Invoice Payment", Description = "M2M scope for POS Service to confirm a completed terminal payment to InvoiceService." },
            new { Name = "catalog.validate", DisplayName = "Validate Catalog Items", Description = "M2M scope for BasketService to validate items/prices from CatalogService." },
            new { Name = "didarsync.read", DisplayName = "Read DidarSync", Description = "M2M scope for reading DidarSync operational endpoints." },
            new { Name = "didarsync.write", DisplayName = "Write DidarSync", Description = "M2M scope for managing DidarSync product/order sync and outbox retry endpoints." },
            new { Name = "event.publish", DisplayName = "Publish Integration Events", Description = "M2M scope for services that publish signed integration events." },
            new { Name = "event.consume", DisplayName = "Consume Integration Events", Description = "M2M scope for services that validate and consume signed integration events." },
            
            // Legacy scopes (deprecated, kept for backward compatibility)
            new { Name = "sepidar.read", DisplayName = "Read Sepidar (Legacy)", Description = "[DEPRECATED] Use domain-specific scopes instead." },
            new { Name = "sepidar.write", DisplayName = "Write Sepidar (Legacy)", Description = "[DEPRECATED] Use domain-specific scopes instead." }
        };

        foreach (var s in scopes)
        {
            var existing = scopeRepo.Query().FirstOrDefault(x => x.Name == s.Name);
            if (existing is null)
            {
                await scopeRepo.AddAsync(new Scope
                {
                    Id = Guid.NewGuid(),
                    Name = s.Name,
                    DisplayName = s.DisplayName,
                    Description = s.Description,
                    CreatedAt = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await SeedScopePermissionsAsync(cancellationToken);
    }

    private async Task SeedScopePermissionsAsync(CancellationToken cancellationToken)
    {
        var scopeRepo = _unitOfWork.Repository<Scope>();
        var permissionRepo = _unitOfWork.Repository<Permission>();
        var scopePermissionRepo = _unitOfWork.Repository<ScopePermission>();

        var scopes = scopeRepo.Query().ToDictionary(s => s.Name, s => s.Id);
        var permissions = permissionRepo.Query().ToDictionary(p => p.Key, p => p.Id);

        void Map(string scopeName, IEnumerable<string> permissionKeys)
        {
            if (!scopes.TryGetValue(scopeName, out var scopeId))
            {
                return;
            }

            foreach (var key in permissionKeys)
            {
                if (!permissions.TryGetValue(key, out var pid))
                {
                    continue;
                }

                var exists = scopePermissionRepo.Query()
                    .Any(sp => sp.ScopeId == scopeId && sp.PermissionId == pid);
                if (!exists)
                {
                    scopePermissionRepo.AddAsync(new ScopePermission
                    {
                        ScopeId = scopeId,
                        PermissionId = pid
                    }, cancellationToken).GetAwaiter().GetResult();
                }
            }
        }

        Map("identity.self", new[]
        {
            "Identity.Profile.Get",
            "Identity.Profile.Put",
            "Identity.Token.Refresh",
            "Identity.Token.Logout"
        });

        Map("identity.mcp.self.read", new[]
        {
            "Identity.Mcp.Self.Read"
        });

        Map("identity.mcp.self.write", new[]
        {
            "Identity.Mcp.Self.Write"
        });

        Map("identity.mcp.admin.read", new[]
        {
            "Identity.Mcp.Admin.Read"
        });

        Map("identity.mcp.admin.write", new[]
        {
            "Identity.Mcp.Admin.Write"
        });

        Map("identity.mcp.security.read", new[]
        {
            "Identity.Mcp.Security.Read"
        });

        Map("identity.mcp.security.write", new[]
        {
            "Identity.Mcp.Security.Write"
        });

        // Capability-level MCP grants. There is no implicit mapping from an API
        // permission or the legacy umbrella scopes to these scopes.
        var mcpParityMappings = new (string Scope, string Permission)[]
        {
            ("identity.mcp.profile.read", "Identity.Mcp.Profile.Read"),
            ("identity.mcp.profile.write", "Identity.Mcp.Profile.Write"),
            ("identity.mcp.sessions.read", "Identity.Mcp.Sessions.Read"),
            ("identity.mcp.sessions-revoke.sensitive", "Identity.Mcp.SessionsRevoke.Sensitive"),
            ("identity.mcp.sessions-revoke-others.sensitive", "Identity.Mcp.SessionsRevokeOthers.Sensitive"),
            ("identity.mcp.user-access-summary.read", "Identity.Mcp.UserAccessSummary.Read"),
            ("identity.mcp.sessions-admin.sensitive", "Identity.Mcp.SessionsAdmin.Sensitive"),
            ("identity.mcp.users.read", "Identity.Mcp.Users.Read"),
            ("identity.mcp.users.write", "Identity.Mcp.Users.Write"),
            ("identity.mcp.users.sensitive", "Identity.Mcp.Users.Sensitive"),
            ("identity.mcp.roles.read", "Identity.Mcp.Roles.Read"),
            ("identity.mcp.roles.write", "Identity.Mcp.Roles.Write"),
            ("identity.mcp.roles.sensitive", "Identity.Mcp.Roles.Sensitive"),
            ("identity.mcp.tenants.read", "Identity.Mcp.Tenants.Read"),
            ("identity.mcp.tenants.write", "Identity.Mcp.Tenants.Write"),
            ("identity.mcp.tenants.sensitive", "Identity.Mcp.Tenants.Sensitive"),
            ("identity.mcp.permissions.read", "Identity.Mcp.Permissions.Read"),
            ("identity.mcp.permissions.write", "Identity.Mcp.Permissions.Write"),
            ("identity.mcp.permissions.sensitive", "Identity.Mcp.Permissions.Sensitive"),
            ("identity.mcp.scopes.read", "Identity.Mcp.Scopes.Read"),
            ("identity.mcp.scopes.write", "Identity.Mcp.Scopes.Write"),
            ("identity.mcp.scopes.sensitive", "Identity.Mcp.Scopes.Sensitive"),
            ("identity.mcp.clients.read", "Identity.Mcp.Clients.Read"),
            ("identity.mcp.clients.write", "Identity.Mcp.Clients.Write"),
            ("identity.mcp.user-roles.read", "Identity.Mcp.UserRoles.Read"),
            ("identity.mcp.user-roles.sensitive", "Identity.Mcp.UserRoles.Sensitive"),
            ("identity.mcp.user-tenants.read", "Identity.Mcp.UserTenants.Read"),
            ("identity.mcp.user-tenants.sensitive", "Identity.Mcp.UserTenants.Sensitive"),
            ("identity.mcp.role-permissions.read", "Identity.Mcp.RolePermissions.Read"),
            ("identity.mcp.role-permissions.sensitive", "Identity.Mcp.RolePermissions.Sensitive"),
            ("identity.mcp.scope-permissions.read", "Identity.Mcp.ScopePermissions.Read"),
            ("identity.mcp.scope-permissions.sensitive", "Identity.Mcp.ScopePermissions.Sensitive"),
            ("identity.mcp.client-scopes.read", "Identity.Mcp.ClientScopes.Read"),
            ("identity.mcp.client-scopes.sensitive", "Identity.Mcp.ClientScopes.Sensitive"),
            ("identity.mcp.role-users.read", "Identity.Mcp.RoleUsers.Read"),
            ("identity.mcp.tenant-users.read", "Identity.Mcp.TenantUsers.Read"),
            ("identity.mcp.protocol.refresh", "Identity.Mcp.Protocol.Refresh"),
            ("identity.mcp.protocol.logout", "Identity.Mcp.Protocol.Logout"),
            ("identity.mcp.protocol.client-secrets.read", "Identity.Mcp.Protocol.ClientSecrets.Read"),
            ("identity.mcp.protocol.client-secrets.write", "Identity.Mcp.Protocol.ClientSecrets.Write"),
            ("identity.mcp.protocol.client-secrets.revoke", "Identity.Mcp.Protocol.ClientSecrets.Revoke")
        };

        foreach (var (scopeName, permissionKey) in mcpParityMappings)
        {
            Map(scopeName, new[] { permissionKey });
        }

        Map("customer.self", new[]
        {
            "Customer.Me.Customer.Get",
            "Customer.Me.Customer.Post",
            "Customer.Me.Customer.Put",
            "Customer.Me.Addresses.Get",
            "Customer.Me.Addresses.Post",
            "Customer.Me.Addresses.Put",
            "Customer.Me.Addresses.Delete"
        });

        Map("basket.self", new[]
        {
            "Basket.Me.Get",
            "Basket.Me.Post",
            "Basket.Me.Put",
            "Basket.Me.Delete",
            "Basket.Me.Clear",
            "Basket.Me.Checkout"
        });

        Map("order.self", new[]
        {
            "Order.Me.Get",
            "Order.Me.GetById"
        });

        Map("invoice.self", new[]
        {
            "Invoice.Me.Get",
            "Invoice.Me.GetById"
        });

        Map("payment.self", new[]
        {
            "Payment.Me.Get",
            "Payment.Me.GetById",
            "Payment.Start",
            "Payment.Verify"
        });

        // M2M scope-permission mappings
        Map("otp.send", new[]
        {
            "Otp.Send",
            "Otp.Verify"
        });

        Map("notification.send", new[]
        {
            "Notification.Send"
        });

        Map("customer.read", new[]
        {
            "Customer.ByPhone.Get"
        });

        Map("customer.write", new[]
        {
            "Customer.Admin.List",
            "Customer.Admin.Get"
        });

        Map("sepidar.customers.read", new[]
        {
            "Sepidar.Customers.Read"
        });

        Map("sepidar.customers.write", new[]
        {
            "Sepidar.Customers.Write"
        });

        Map("sepidar.catalog.read", new[]
        {
            "Sepidar.Catalog.Read"
        });

        Map("sepidar.orders.read", new[]
        {
            "Sepidar.Orders.Read"
        });

        Map("sepidar.orders.write", new[]
        {
            "Sepidar.Orders.Write"
        });

        Map("sepidar.invoices.read", new[]
        {
            "Sepidar.Invoices.Read"
        });

        Map("sepidar.invoices.write", new[]
        {
            "Sepidar.Invoices.Write"
        });

        Map("sepidar.common.read", new[]
        {
            "Sepidar.Common.Read"
        });

        // Service-to-Service scope mappings
        Map("orders.read", new[]
        {
            "Orders.Read"
        });

        Map("orders.status", new[]
        {
            "Orders.Status.Update"
        });

        Map("invoices.create", new[]
        {
            "Invoices.Create"
        });

        Map("invoices.read", new[]
        {
            "Invoice.Read"
        });

        Map("catalog.validate", new[]
        {
            "Catalog.Validate"
        });

        Map("didarsync.read", new[]
        {
            "DidarSync.Outbox.Get"
        });

        Map("didarsync.write", new[]
        {
            "DidarSync.Outbox.Get",
            "DidarSync.Outbox.Retry",
            "DidarSync.Products.Sync",
            "DidarSync.Orders.Sync"
        });

        // Legacy scope mappings (deprecated but kept for backward compatibility)
        Map("sepidar.read", new[]
        {
            "Sepidar.Read"
        });

        Map("sepidar.write", new[]
        {
            "Sepidar.Write"
        });

        Map("catalog.read", new[]
        {
            "Catalog.Product.ByCode.Get",
            "Catalog.Product.WithoutImageExcel.Get"
        });

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedClientAsync(CancellationToken cancellationToken)
    {
        await SeedPublicClientAsync(cancellationToken);
        await SeedM2MClientsAsync(cancellationToken);
    }

    private async Task SeedPublicClientAsync(CancellationToken cancellationToken)
    {
        var defaultClientPublicId = _clientOptions.DefaultPublicClientId?.Trim();
        if (string.IsNullOrWhiteSpace(defaultClientPublicId))
        {
            return;
        }

        var clientRepo = _unitOfWork.Repository<Client>();
        var scopeRepo = _unitOfWork.Repository<Scope>();
        var clientScopeRepo = _unitOfWork.Repository<ClientScope>();

        var client = clientRepo.Query().FirstOrDefault(c => c.ClientId == defaultClientPublicId);
        if (client is null)
        {
            client = new Client
            {
                Id = Guid.NewGuid(),
                ClientId = defaultClientPublicId,
                Name = "Public Web Client",
                Description = "Default public client for OTP-based login.",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await clientRepo.AddAsync(client, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var scopes = scopeRepo.Query()
            .Where(s => s.Name == "identity.self" || s.Name == "identity.mcp.self.read" || s.Name == "identity.mcp.profile.read" || s.Name == "identity.mcp.sessions.read" || s.Name == "customer.self" || s.Name == "catalog.read"
                     || s.Name == "basket.self" || s.Name == "order.self" || s.Name == "invoice.self" || s.Name == "payment.self")
            .ToList();

        foreach (var scope in scopes)
        {
            var exists = clientScopeRepo.Query()
                .Any(cs => cs.ClientId == client.Id && cs.ScopeId == scope.Id);
            if (!exists)
            {
                await clientScopeRepo.AddAsync(new ClientScope
                {
                    ClientId = client.Id,
                    ScopeId = scope.Id
                }, cancellationToken);
            }
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedM2MClientsAsync(CancellationToken cancellationToken)
    {
        var m2mClients = _clientOptions.M2MClients;
        if (m2mClients is null || m2mClients.Count == 0)
        {
            _logger.LogWarning("⚠️ No M2M clients configured in IdentityClient:M2MClients");
            return;
        }

        _logger.LogInformation("🔷 Starting M2M clients seeding. Found {Count} clients to process.", m2mClients.Count);

        var clientRepo = _unitOfWork.Repository<Client>();
        var secretRepo = _unitOfWork.Repository<ClientSecret>();
        var scopeRepo = _unitOfWork.Repository<Scope>();
        var clientScopeRepo = _unitOfWork.Repository<ClientScope>();

        int successCount = 0;
        int skipCount = 0;

        foreach (var (clientId, config) in m2mClients)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    _logger.LogWarning("⚠️ Skipping M2M client with null/empty ClientId");
                    skipCount++;
                    continue;
                }

                if (string.IsNullOrWhiteSpace(config.Secret))
                {
                    if (!string.IsNullOrWhiteSpace(config.RequiredSecretKey))
                    {
                        if (config.Optional)
                        {
                            _logger.LogWarning("⚠️ Skipping optional M2M client '{ClientId}' because secret '{SecretKey}' is not configured.", clientId, config.RequiredSecretKey);
                            skipCount++;
                            continue;
                        }

                        throw new InvalidOperationException(
                            $"Required M2M secret '{config.RequiredSecretKey}' is not configured for client '{clientId}'.");
                    }

                    _logger.LogWarning("⚠️ Skipping M2M client '{ClientId}' - no secret provided in environment", clientId);
                    skipCount++;
                    continue;
                }

                // Create or get client
                var client = clientRepo.Query().FirstOrDefault(c => c.ClientId == clientId);
                if (client is null)
                {
                    _logger.LogInformation("📝 Creating new M2M client: {ClientId}", clientId);
                    client = new Client
                    {
                        Id = Guid.NewGuid(),
                        ClientId = clientId,
                        Name = config.Name,
                        Description = config.Description,
                        IsActive = true,
                        CreatedAt = DateTime.UtcNow
                    };

                    await clientRepo.AddAsync(client, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("✅ M2M client created: {ClientId} (ID: {Id})", clientId, client.Id);
                }
                else
                {
                    _logger.LogInformation("ℹ️ M2M client already exists: {ClientId} (ID: {Id})", clientId, client.Id);
                }

                // Create secret if not exists
                var existingSecrets = secretRepo.Query()
                    .Where(s => s.ClientId == client.Id && s.RevokedAt == null)
                    .ToList();

                var secretHash = HashSecret(config.Secret);
                var hasMatchingSecret = existingSecrets.Any(s => s.Hash == secretHash);

                if (!hasMatchingSecret)
                {
                    _logger.LogInformation("🔐 Adding new secret for client: {ClientId}", clientId);

                    // Revoke old secrets
                    foreach (var oldSecret in existingSecrets)
                    {
                        oldSecret.RevokedAt = DateTime.UtcNow;
                        _logger.LogInformation("📌 Revoked old secret for client: {ClientId}", clientId);
                    }

                    // Create new secret
                    var secret = new ClientSecret
                    {
                        Id = Guid.NewGuid(),
                        ClientId = client.Id,
                        Hash = secretHash,
                        CreatedAt = DateTime.UtcNow,
                        ExpiresAt = null // M2M secrets don't expire by default
                    };

                    await secretRepo.AddAsync(secret, cancellationToken);
                    await _unitOfWork.SaveChangesAsync(cancellationToken);
                    _logger.LogInformation("✅ Secret created for client: {ClientId}", clientId);
                }
                else
                {
                    _logger.LogInformation("ℹ️ Secret already exists for client: {ClientId}", clientId);
                }

                // Assign scopes
                if (config.Scopes.Length > 0)
                {
                    // Avoid parameterized collection translation (OPENJSON ... WITH),
                    // which is unsupported by older SQL Server compatibility levels.
                    var scopes = new List<Scope>();
                    foreach (var scopeName in config.Scopes
                                 .Where(name => !string.IsNullOrWhiteSpace(name))
                                 .Distinct(StringComparer.OrdinalIgnoreCase))
                    {
                        var scope = scopeRepo.Query().FirstOrDefault(s => s.Name == scopeName);
                        if (scope is not null)
                        {
                            scopes.Add(scope);
                        }
                    }

                    _logger.LogInformation("📌 Assigning {ScopeCount} scopes to client {ClientId}: {Scopes}",
                        scopes.Count, clientId, string.Join(", ", config.Scopes));

                    foreach (var scope in scopes)
                    {
                        var exists = clientScopeRepo.Query()
                            .Any(cs => cs.ClientId == client.Id && cs.ScopeId == scope.Id);
                        if (!exists)
                        {
                            await clientScopeRepo.AddAsync(new ClientScope
                            {
                                ClientId = client.Id,
                                ScopeId = scope.Id
                            }, cancellationToken);
                            _logger.LogInformation("✅ Added scope '{ScopeName}' to client {ClientId}", scope.Name, clientId);
                        }
                        else
                        {
                            _logger.LogInformation("ℹ️ Scope '{ScopeName}' already assigned to client {ClientId}", scope.Name, clientId);
                        }
                    }

                    if (scopes.Count != config.Scopes.Length)
                    {
                        var missingScopes = config.Scopes.Where(s => !scopes.Any(sc => sc.Name == s)).ToArray();
                        if (!config.Optional && !string.IsNullOrWhiteSpace(config.RequiredSecretKey))
                        {
                            throw new InvalidOperationException(
                                $"Required scopes are missing for M2M client '{clientId}': {string.Join(", ", missingScopes)}.");
                        }

                        _logger.LogWarning("⚠️ Some scopes not found in database for client {ClientId}: {MissingScopes}",
                            clientId, string.Join(", ", missingScopes));
                    }
                }
                else
                {
                    _logger.LogWarning("⚠️ No scopes configured for client: {ClientId}", clientId);
                }

                successCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Error seeding M2M client '{ClientId}': {Message}", clientId, ex.Message);
                throw;
            }
        }

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Error saving M2M clients to database: {Message}", ex.Message);
            throw;
        }

        _logger.LogInformation("✅ M2M clients seeding completed. Success: {SuccessCount}, Skipped: {SkipCount}", successCount, skipCount);
    }

    private static string HashSecret(string value)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(value);
        var hashBytes = System.Security.Cryptography.SHA256.HashData(bytes);
        return Convert.ToBase64String(hashBytes);
    }
}
