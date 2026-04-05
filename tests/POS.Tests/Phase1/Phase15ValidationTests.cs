using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using POS.Application.Abstractions;
using POS.Application.Exceptions;
using POS.Application.Models;
using POS.Domain.Entities;
using POS.Domain.Enums;
using POS.Infrastructure.Data;
using POS.Infrastructure.Printing;
using POS.Infrastructure.Security;
using POS.Infrastructure.Services;

namespace POS.Tests.Phase1;

public sealed class Phase15ValidationTests
{
    [Fact]
    public async Task RapidBarcodeScans_Should_Be_Consistent_And_Fast()
    {
        var databasePath = BuildTempPath("rapid-scans", "pos.db");

        try
        {
            await using var setupContext = await CreateSqliteContextAsync(databasePath);

            var cashier = new User
            {
                Username = "cashier",
                PasswordHash = "hash",
                FullName = "Cashier",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            setupContext.Users.Add(cashier);

            const int productCount = 5000;
            var products = new List<Product>(productCount);
            for (var i = 1; i <= productCount; i += 1)
            {
                products.Add(
                    new Product
                    {
                        Name = $"Product-{i}",
                        Barcode = $"9000000{i:000000}",
                        Price = 10m + (i % 100),
                        Cost = 5m + (i % 50),
                        QuantityOnHand = 100,
                        ReorderLevel = 5,
                        IsActive = true,
                    });
            }

            setupContext.Products.AddRange(products);
            await setupContext.SaveChangesAsync();

            var barcodeToProductId = products.ToDictionary(x => x.Barcode, x => x.Id);
            var scanSequence = products.Take(1000).Select(x => x.Barcode).Concat(products.Take(1000).Select(x => x.Barcode)).ToArray();

            var service = BuildCheckoutService(setupContext, new NoOpCheckoutExecutionHook());
            var cart = new Dictionary<string, int>(StringComparer.Ordinal);

            var scanStopwatch = System.Diagnostics.Stopwatch.StartNew();

            foreach (var barcode in scanSequence)
            {
                Assert.True(barcodeToProductId.ContainsKey(barcode), $"Missing barcode lookup for {barcode}.");
                cart.TryGetValue(barcode, out var currentQuantity);
                cart[barcode] = currentQuantity + 1;
            }

            scanStopwatch.Stop();

            var checkoutStopwatch = System.Diagnostics.Stopwatch.StartNew();

            var checkoutRequest = new CheckoutRequest
            {
                UserId = cashier.Id,
                IdempotencyKey = $"rapid-{Guid.NewGuid():N}",
                TaxRatePercent = 5m,
                PaymentMethod = PaymentMethod.Cash,
                AmountTendered = 999999m,
                Items = cart
                    .Select(
                        x => new CheckoutItemRequest
                        {
                            ProductId = barcodeToProductId[x.Key],
                            Quantity = x.Value,
                        })
                    .ToArray(),
            };

            var checkoutResponse = await service.ProcessCheckoutAsync(checkoutRequest);
            checkoutStopwatch.Stop();

            Assert.Equal(scanSequence.Length, cart.Values.Sum());
            Assert.False(checkoutResponse.IsIdempotentReplay);

            var averageScanMilliseconds = scanStopwatch.Elapsed.TotalMilliseconds / scanSequence.Length;
            Assert.True(averageScanMilliseconds < 0.25, $"Average scan processing was {averageScanMilliseconds:0.000}ms per scan.");
            Assert.True(checkoutStopwatch.ElapsedMilliseconds < 3500, $"Large-cart checkout commit took {checkoutStopwatch.ElapsedMilliseconds}ms.");

            var updatedProduct = await setupContext.Products.SingleAsync(x => x.Id == products[0].Id);
            Assert.Equal(98, updatedProduct.QuantityOnHand);
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    [Fact]
    public async Task DuplicatePaymentSubmissions_Should_Not_Create_DuplicateTransactions()
    {
        var databasePath = BuildTempPath("idempotency", "pos.db");

        try
        {
            await using var context = await CreateSqliteContextAsync(databasePath);

            var cashier = new User
            {
                Username = "cashier-idempotency",
                PasswordHash = "hash",
                FullName = "Cashier",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            var product = new Product
            {
                Name = "Scanner Item",
                Barcode = "5000000000001",
                Price = 25m,
                Cost = 8m,
                QuantityOnHand = 200,
                ReorderLevel = 5,
                IsActive = true,
            };

            context.Users.Add(cashier);
            context.Products.Add(product);
            await context.SaveChangesAsync();

            const int rapidSubmissions = 25;
            var idempotencyKey = $"dup-pay-{Guid.NewGuid():N}";

            var responses = new List<CheckoutResponse>(rapidSubmissions);
            for (var i = 0; i < rapidSubmissions; i += 1)
            {
                await using var operationContext = await CreateSqliteContextAsync(databasePath);
                var service = BuildCheckoutService(operationContext, new NoOpCheckoutExecutionHook());
                var response = await service.ProcessCheckoutAsync(
                    new CheckoutRequest
                    {
                        UserId = cashier.Id,
                        IdempotencyKey = idempotencyKey,
                        TaxRatePercent = 10m,
                        PaymentMethod = PaymentMethod.Cash,
                        AmountTendered = 100m,
                        Items =
                        [
                            new CheckoutItemRequest
                            {
                                ProductId = product.Id,
                                Quantity = 1,
                            },
                        ],
                    });

                responses.Add(response);
            }

            var distinctSalesOrderIds = responses.Select(x => x.SalesOrderId).Distinct().ToArray();
            Assert.Single(distinctSalesOrderIds);
            Assert.Equal(1, responses.Count(x => !x.IsIdempotentReplay));
            Assert.Equal(rapidSubmissions - 1, responses.Count(x => x.IsIdempotentReplay));

            await using var verificationContext = await CreateSqliteContextAsync(databasePath);
            var orderCount = await verificationContext.SalesOrders.CountAsync();
            Assert.Equal(1, orderCount);

            var updatedProduct = await verificationContext.Products.SingleAsync(x => x.Id == product.Id);
            Assert.Equal(199, updatedProduct.QuantityOnHand);
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    [Fact]
    public async Task CrashDuringCheckout_Should_Rollback_And_Remain_Consistent_AfterRestart()
    {
        var databasePath = BuildTempPath("crash-recovery", "pos.db");

        try
        {
            await using var setupContext = await CreateSqliteContextAsync(databasePath);

            var cashier = new User
            {
                Username = "cashier-crash",
                PasswordHash = "hash",
                FullName = "Cashier",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            var product = new Product
            {
                Name = "Crash Test Item",
                Barcode = "5000000000002",
                Price = 15m,
                Cost = 6m,
                QuantityOnHand = 30,
                ReorderLevel = 5,
                IsActive = true,
            };

            setupContext.Users.Add(cashier);
            setupContext.Products.Add(product);
            await setupContext.SaveChangesAsync();

            var crashingService = BuildCheckoutService(setupContext, new ThrowingCheckoutExecutionHook());

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => crashingService.ProcessCheckoutAsync(
                    new CheckoutRequest
                    {
                        UserId = cashier.Id,
                        IdempotencyKey = $"crash-{Guid.NewGuid():N}",
                        TaxRatePercent = 5m,
                        PaymentMethod = PaymentMethod.Cash,
                        AmountTendered = 100m,
                        Items =
                        [
                            new CheckoutItemRequest
                            {
                                ProductId = product.Id,
                                Quantity = 2,
                            },
                        ],
                    }));

            await setupContext.DisposeAsync();

            await using var restartContext = await CreateSqliteContextAsync(databasePath);
            var reloadedProduct = await restartContext.Products.SingleAsync(x => x.Barcode == "5000000000002");
            Assert.Equal(30, reloadedProduct.QuantityOnHand);

            var orderCount = await restartContext.SalesOrders.CountAsync();
            Assert.Equal(0, orderCount);

            var adjustmentCount = await restartContext.InventoryAdjustments.CountAsync();
            Assert.Equal(0, adjustmentCount);
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    [Fact]
    public async Task PrintingFailures_Should_Be_Stable_With_Retry_And_Reprint()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new PosDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var user = new User
        {
            Username = "cashier-print",
            PasswordHash = "hash",
            FullName = "Cashier",
            Role = UserRole.Cashier,
            IsActive = true,
        };

        var order = new SalesOrder
        {
            ReceiptNumber = "R-PRINT-0001",
            IdempotencyKey = "idem-print-check",
            User = user,
            OrderType = SalesOrderType.Sale,
            TotalBeforeDiscount = 12m,
            DiscountAmount = 0m,
            TaxAmount = 0m,
            TotalAfterTax = 12m,
            PaymentMethod = PaymentMethod.Cash,
            AmountTendered = 12m,
        };

        var receipt = new Receipt
        {
            SalesOrder = order,
            ReceiptNumber = "R-PRINT-0001",
            ThermalPayload = "receipt payload",
            PayloadHash = "hash",
        };

        context.Receipts.Add(receipt);
        await context.SaveChangesAsync();

        var alwaysFailGateway = new AlwaysFailReceiptPrinterGateway();
        var failingService = new ReceiptPrintService(
            context,
            alwaysFailGateway,
            Options.Create(new ReceiptPrintingOptions { MaxRetryAttempts = 3, RetryDelayMilliseconds = 0 }),
            new AuditLogService(context),
            NullLogger<ReceiptPrintService>.Instance);

        await Assert.ThrowsAsync<AppValidationException>(() => failingService.PrintReceiptAsync(receipt.Id, isReprint: false));
        Assert.Equal(3, alwaysFailGateway.Attempts);

        var successGateway = new SuccessfulReceiptPrinterGateway();
        var recoveryService = new ReceiptPrintService(
            context,
            successGateway,
            Options.Create(new ReceiptPrintingOptions { MaxRetryAttempts = 2, RetryDelayMilliseconds = 0 }),
            new AuditLogService(context),
            NullLogger<ReceiptPrintService>.Instance);

        await recoveryService.ReprintByReceiptNumberAsync("R-PRINT-0001");

        var successfulReprints = await context.ReceiptPrintLogs
            .Where(x => x.ReceiptId == receipt.Id && x.IsReprint && x.IsSuccessful)
            .CountAsync();

        Assert.Equal(1, successfulReprints);
    }

    [Fact]
    public async Task SessionHandling_Should_Support_Persistence_And_AutoLogout()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new PosDbContext(options);
        await context.Database.EnsureCreatedAsync();

        var user = new User
        {
            Username = "cashier-session",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password_123!", 12),
            FullName = "Cashier",
            Role = UserRole.Cashier,
            IsActive = true,
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();

        var securityOptions = Options.Create(
            new SecurityOptions
            {
                JwtSigningKey = "SESSION_TESTING_SIGNING_KEY_1234567890",
                JwtIssuer = "POS.Test",
                JwtAudience = "POS.Test.Client",
                TokenLifetimeSeconds = 3,
            });

        var authService = new AuthService(
            context,
            new TestClock(),
            securityOptions,
            new AuditLogService(context),
            NullLogger<AuthService>.Instance);

        var loginResponse = await authService.LoginAsync(
            new LoginRequest
            {
                Username = "cashier-session",
                Password = "Password_123!",
            });

        Assert.True(loginResponse.IsAuthenticated);
        Assert.False(string.IsNullOrWhiteSpace(loginResponse.Token));

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityOptions.Value.JwtSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = securityOptions.Value.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = securityOptions.Value.JwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
        };

        _ = tokenHandler.ValidateToken(loginResponse.Token, tokenValidationParameters, out var validatedToken);
        Assert.NotNull(validatedToken);

        await Task.Delay(3500);

        await Assert.ThrowsAsync<SecurityTokenExpiredException>(
            () => Task.Run(
                () => tokenHandler.ValidateToken(loginResponse.Token, tokenValidationParameters, out _)));
    }

    [Fact]
    public async Task HighFrequencyTransactions_With_LargeCatalog_Should_Meet_Performance_Target()
    {
        var databasePath = BuildTempPath("high-frequency", "pos.db");

        try
        {
            await using var setupContext = await CreateSqliteContextAsync(databasePath);

            var cashier = new User
            {
                Username = "cashier-perf",
                PasswordHash = "hash",
                FullName = "Cashier",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            setupContext.Users.Add(cashier);

            const int productCount = 10000;
            var products = new List<Product>(productCount);
            for (var i = 1; i <= productCount; i += 1)
            {
                products.Add(
                    new Product
                    {
                        Name = $"Perf-{i}",
                        Barcode = $"8000000{i:000000}",
                        Price = 9m + (i % 20),
                        Cost = 4m + (i % 10),
                        QuantityOnHand = 500,
                        ReorderLevel = 5,
                        IsActive = true,
                    });
            }

            setupContext.Products.AddRange(products);
            await setupContext.SaveChangesAsync();

            var productIds = products.Take(300).Select(x => x.Id).ToArray();
            var random = new Random(123);

            const int transactionCount = 120;
            var elapsed = System.Diagnostics.Stopwatch.StartNew();

            for (var i = 0; i < transactionCount; i += 1)
            {
                await using var checkoutContext = await CreateSqliteContextAsync(databasePath);
                var checkoutService = BuildCheckoutService(checkoutContext, new NoOpCheckoutExecutionHook());

                var selectedProductId = productIds[random.Next(productIds.Length)];

                await checkoutService.ProcessCheckoutAsync(
                    new CheckoutRequest
                    {
                        UserId = cashier.Id,
                        IdempotencyKey = $"perf-{i}-{Guid.NewGuid():N}",
                        TaxRatePercent = 5m,
                        PaymentMethod = PaymentMethod.Cash,
                        AmountTendered = 100m,
                        Items =
                        [
                            new CheckoutItemRequest
                            {
                                ProductId = selectedProductId,
                                Quantity = 1,
                            },
                        ],
                    });
            }

            elapsed.Stop();
            var averageMilliseconds = elapsed.Elapsed.TotalMilliseconds / transactionCount;

            Assert.True(averageMilliseconds < 200, $"Average transaction time was {averageMilliseconds:0.00}ms.");

            await using var verificationContext = await CreateSqliteContextAsync(databasePath);
            var orderCount = await verificationContext.SalesOrders.CountAsync();
            Assert.Equal(transactionCount, orderCount);
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    [Fact]
    public async Task StressTests_CheckoutService_Should_Handle_100_Rapid_Transactions()
    {
        var databasePath = BuildTempPath("stress-100-transactions", "pos.db");

        try
        {
            await using var setupContext = await CreateSqliteContextAsync(databasePath);

            var cashier = new User
            {
                Username = "cashier-stress-100",
                PasswordHash = "hash",
                FullName = "Cashier",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            var product = new Product
            {
                Name = "Stress Product",
                Barcode = "7000000000001",
                Price = 10m,
                Cost = 4m,
                QuantityOnHand = 300,
                ReorderLevel = 5,
                IsActive = true,
            };

            setupContext.Users.Add(cashier);
            setupContext.Products.Add(product);
            await setupContext.SaveChangesAsync();

            const int transactionCount = 100;

            for (var i = 0; i < transactionCount; i += 1)
            {
                await using var operationContext = await CreateSqliteContextAsync(databasePath);
                var service = BuildCheckoutService(operationContext, new NoOpCheckoutExecutionHook());

                var response = await service.ProcessCheckoutAsync(
                    new CheckoutRequest
                    {
                        UserId = cashier.Id,
                        IdempotencyKey = $"stress-100-{i}-{Guid.NewGuid():N}",
                        TaxRatePercent = 5m,
                        PaymentMethod = PaymentMethod.Cash,
                        AmountTendered = 50m,
                        Items =
                        [
                            new CheckoutItemRequest
                            {
                                ProductId = product.Id,
                                Quantity = 1,
                            },
                        ],
                    });

                Assert.False(response.IsIdempotentReplay);
            }

            await using var verificationContext = await CreateSqliteContextAsync(databasePath);
            var orderCount = await verificationContext.SalesOrders.CountAsync();
            Assert.Equal(transactionCount, orderCount);

            var updatedProduct = await verificationContext.Products.SingleAsync(x => x.Id == product.Id);
            Assert.Equal(200, updatedProduct.QuantityOnHand);
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    [Fact]
    public async Task StressTests_CheckoutService_Should_Handle_Large_Cart_Of_100_Items()
    {
        var databasePath = BuildTempPath("stress-large-cart", "pos.db");

        try
        {
            await using var context = await CreateSqliteContextAsync(databasePath);

            var cashier = new User
            {
                Username = "cashier-large-cart",
                PasswordHash = "hash",
                FullName = "Cashier",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            context.Users.Add(cashier);

            var products = Enumerable.Range(1, 100)
                .Select(
                    i => new Product
                    {
                        Name = $"CartProduct-{i}",
                        Barcode = $"7100000{i:000000}",
                        Price = 5m + (i % 10),
                        Cost = 2m + (i % 5),
                        QuantityOnHand = 25,
                        ReorderLevel = 5,
                        IsActive = true,
                    })
                .ToArray();

            context.Products.AddRange(products);
            await context.SaveChangesAsync();

            var service = BuildCheckoutService(context, new NoOpCheckoutExecutionHook());
            var response = await service.ProcessCheckoutAsync(
                new CheckoutRequest
                {
                    UserId = cashier.Id,
                    IdempotencyKey = $"large-cart-{Guid.NewGuid():N}",
                    TaxRatePercent = 5m,
                    PaymentMethod = PaymentMethod.Cash,
                    AmountTendered = 5000m,
                    Items = products
                        .Select(
                            x => new CheckoutItemRequest
                            {
                                ProductId = x.Id,
                                Quantity = 1,
                            })
                        .ToArray(),
                });

            Assert.False(response.IsIdempotentReplay);

            var persistedLineCount = await context.SalesOrderLines.CountAsync(x => x.SalesOrderId == response.SalesOrderId);
            Assert.Equal(100, persistedLineCount);
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    [Fact]
    public async Task StressTests_CheckoutService_Should_Handle_Concurrent_Stock_Updates()
    {
        var databasePath = BuildTempPath("stress-concurrent-stock", "pos.db");

        try
        {
            await using var setupContext = await CreateSqliteContextAsync(databasePath);

            var cashier = new User
            {
                Username = "cashier-concurrency",
                PasswordHash = "hash",
                FullName = "Cashier",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            var product = new Product
            {
                Name = "Concurrent Product",
                Barcode = "7200000000001",
                Price = 30m,
                Cost = 12m,
                QuantityOnHand = 1,
                ReorderLevel = 1,
                IsActive = true,
            };

            setupContext.Users.Add(cashier);
            setupContext.Products.Add(product);
            await setupContext.SaveChangesAsync();

            var startGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            Task<Exception?> attemptCheckout(string key)
            {
                return Task.Run(
                    async () =>
                    {
                        await using var operationContext = await CreateSqliteContextAsync(databasePath);
                        var service = BuildCheckoutService(operationContext, new NoOpCheckoutExecutionHook());

                        await startGate.Task;

                        try
                        {
                            await service.ProcessCheckoutAsync(
                                new CheckoutRequest
                                {
                                    UserId = cashier.Id,
                                    IdempotencyKey = key,
                                    TaxRatePercent = 0m,
                                    PaymentMethod = PaymentMethod.Cash,
                                    AmountTendered = 100m,
                                    Items =
                                    [
                                        new CheckoutItemRequest
                                        {
                                            ProductId = product.Id,
                                            Quantity = 1,
                                        },
                                    ],
                                });

                            return null;
                        }
                        catch (Exception ex)
                        {
                            return ex;
                        }
                    });
            }

            var firstAttempt = attemptCheckout($"concurrent-1-{Guid.NewGuid():N}");
            var secondAttempt = attemptCheckout($"concurrent-2-{Guid.NewGuid():N}");

            startGate.SetResult(true);

            var results = await Task.WhenAll(firstAttempt, secondAttempt);

            Assert.Equal(1, results.Count(x => x is null));
            Assert.Equal(1, results.Count(x => x is not null));
            Assert.Contains(results, x => x is AppValidationException or DbUpdateException);

            await using var verificationContext = await CreateSqliteContextAsync(databasePath);
            var orderCount = await verificationContext.SalesOrders.CountAsync();
            Assert.Equal(1, orderCount);

            var updatedProduct = await verificationContext.Products.SingleAsync(x => x.Id == product.Id);
            Assert.Equal(0, updatedProduct.QuantityOnHand);
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    [Fact]
    public async Task FailureTests_CheckoutService_Should_Fail_When_Database_Is_Locked()
    {
        var databasePath = BuildTempPath("failure-db-locked", "pos.db");

        try
        {
            await using var setupContext = await CreateSqliteContextAsync(databasePath);

            var cashier = new User
            {
                Username = "cashier-db-locked",
                PasswordHash = "hash",
                FullName = "Cashier",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            var product = new Product
            {
                Name = "Locked Product",
                Barcode = "7300000000001",
                Price = 25m,
                Cost = 10m,
                QuantityOnHand = 10,
                ReorderLevel = 2,
                IsActive = true,
            };

            setupContext.Users.Add(cashier);
            setupContext.Products.Add(product);
            await setupContext.SaveChangesAsync();

            await using var lockConnection = new SqliteConnection($"Data Source={databasePath};Mode=ReadWrite;Cache=Shared;Default Timeout=1");
            await lockConnection.OpenAsync();

            await using var lockCommand = lockConnection.CreateCommand();
            lockCommand.CommandText = "BEGIN EXCLUSIVE;";
            await lockCommand.ExecuteNonQueryAsync();

            try
            {
                var options = new DbContextOptionsBuilder<PosDbContext>()
                    .UseSqlite($"Data Source={databasePath};Default Timeout=1")
                    .EnableSensitiveDataLogging()
                    .Options;

                await using var operationContext = new PosDbContext(options);
                var service = BuildCheckoutService(operationContext, new NoOpCheckoutExecutionHook());

                var exception = await Record.ExceptionAsync(
                    () => service.ProcessCheckoutAsync(
                        new CheckoutRequest
                        {
                            UserId = cashier.Id,
                            IdempotencyKey = $"db-lock-{Guid.NewGuid():N}",
                            TaxRatePercent = 5m,
                            PaymentMethod = PaymentMethod.Cash,
                            AmountTendered = 100m,
                            Items =
                            [
                                new CheckoutItemRequest
                                {
                                    ProductId = product.Id,
                                    Quantity = 1,
                                },
                            ],
                        }));

                Assert.NotNull(exception);
            }
            finally
            {
                await using var unlockCommand = lockConnection.CreateCommand();
                unlockCommand.CommandText = "ROLLBACK;";
                await unlockCommand.ExecuteNonQueryAsync();
            }

            await using var verificationContext = await CreateSqliteContextAsync(databasePath);
            var orderCount = await verificationContext.SalesOrders.CountAsync();
            Assert.Equal(0, orderCount);
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    [Fact]
    public async Task SecurityTests_Cashier_Trying_Admin_Refund_Api_Should_Be_Rejected()
    {
        var databasePath = BuildTempPath("security-cashier-refund", "pos.db");

        try
        {
            await using var context = await CreateSqliteContextAsync(databasePath);

            var cashier = new User
            {
                Username = "cashier-no-refund",
                PasswordHash = "hash",
                FullName = "Cashier",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            context.Users.Add(cashier);
            await context.SaveChangesAsync();

            var refundService = BuildRefundService(context);

            await Assert.ThrowsAsync<UnauthorizedAccessException>(
                () => refundService.CreateRefundAsync(
                    new RefundRequest
                    {
                        SalesOrderId = 1,
                        RequestedByUserId = cashier.Id,
                        Reason = "Unauthorized test",
                    }));
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    [Fact]
    public async Task SecurityTests_Direct_Service_Calls_Should_Block_Idempotency_Key_Reuse_Across_Users()
    {
        var databasePath = BuildTempPath("security-idempotency-reuse", "pos.db");

        try
        {
            await using var setupContext = await CreateSqliteContextAsync(databasePath);

            var firstCashier = new User
            {
                Username = "cashier-a",
                PasswordHash = "hash",
                FullName = "Cashier A",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            var secondCashier = new User
            {
                Username = "cashier-b",
                PasswordHash = "hash",
                FullName = "Cashier B",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            var product = new Product
            {
                Name = "Shared Key Product",
                Barcode = "7400000000001",
                Price = 12m,
                Cost = 5m,
                QuantityOnHand = 20,
                ReorderLevel = 2,
                IsActive = true,
            };

            setupContext.Users.AddRange(firstCashier, secondCashier);
            setupContext.Products.Add(product);
            await setupContext.SaveChangesAsync();

            var sharedKey = $"shared-key-{Guid.NewGuid():N}";

            var firstService = BuildCheckoutService(setupContext, new NoOpCheckoutExecutionHook());
            await firstService.ProcessCheckoutAsync(
                new CheckoutRequest
                {
                    UserId = firstCashier.Id,
                    IdempotencyKey = sharedKey,
                    TaxRatePercent = 0m,
                    PaymentMethod = PaymentMethod.Cash,
                    AmountTendered = 50m,
                    Items =
                    [
                        new CheckoutItemRequest
                        {
                            ProductId = product.Id,
                            Quantity = 1,
                        },
                    ],
                });

            await using var secondContext = await CreateSqliteContextAsync(databasePath);
            var secondService = BuildCheckoutService(secondContext, new NoOpCheckoutExecutionHook());

            await Assert.ThrowsAsync<AppValidationException>(
                () => secondService.ProcessCheckoutAsync(
                    new CheckoutRequest
                    {
                        UserId = secondCashier.Id,
                        IdempotencyKey = sharedKey,
                        TaxRatePercent = 0m,
                        PaymentMethod = PaymentMethod.Cash,
                        AmountTendered = 50m,
                        Items =
                        [
                            new CheckoutItemRequest
                            {
                                ProductId = product.Id,
                                Quantity = 1,
                            },
                        ],
                    }));
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    [Fact]
    public async Task SecurityTests_Invalid_Inputs_Should_Be_Rejected_By_Service_Validation()
    {
        var databasePath = BuildTempPath("security-invalid-inputs", "pos.db");

        try
        {
            await using var context = await CreateSqliteContextAsync(databasePath);

            var admin = new User
            {
                Username = "admin-security",
                PasswordHash = "hash",
                FullName = "Admin",
                Role = UserRole.Admin,
                IsActive = true,
            };

            var cashier = new User
            {
                Username = "cashier-security",
                PasswordHash = "hash",
                FullName = "Cashier",
                Role = UserRole.Cashier,
                IsActive = true,
            };

            var product = new Product
            {
                Name = "Validation Product",
                Barcode = "7500000000001",
                Price = 8m,
                Cost = 3m,
                QuantityOnHand = 10,
                ReorderLevel = 2,
                IsActive = true,
            };

            context.Users.AddRange(admin, cashier);
            context.Products.Add(product);
            await context.SaveChangesAsync();

            var checkoutService = BuildCheckoutService(context, new NoOpCheckoutExecutionHook());

            await Assert.ThrowsAsync<AppValidationException>(
                () => checkoutService.ProcessCheckoutAsync(
                    new CheckoutRequest
                    {
                        UserId = cashier.Id,
                        IdempotencyKey = new string('x', 121),
                        TaxRatePercent = 5m,
                        PaymentMethod = PaymentMethod.Cash,
                        AmountTendered = 50m,
                        Items =
                        [
                            new CheckoutItemRequest
                            {
                                ProductId = product.Id,
                                Quantity = 1,
                            },
                        ],
                    }));

            var refundService = BuildRefundService(context);

            await Assert.ThrowsAsync<AppValidationException>(
                () => refundService.CreateRefundAsync(
                    new RefundRequest
                    {
                        SalesOrderId = 0,
                        RequestedByUserId = admin.Id,
                        Reason = "Invalid request",
                    }));
        }
        finally
        {
            CleanupPath(databasePath);
        }
    }

    private static CheckoutService BuildCheckoutService(PosDbContext context, ICheckoutExecutionHook checkoutExecutionHook)
    {
        return new CheckoutService(
            context,
            new TestClock(),
            new AuditLogService(context),
            checkoutExecutionHook,
            NullLogger<CheckoutService>.Instance);
    }

    private static RefundService BuildRefundService(PosDbContext context)
    {
        return new RefundService(
            context,
            new AuditLogService(context),
            NullLogger<RefundService>.Instance);
    }

    private static async Task<PosDbContext> CreateSqliteContextAsync(string databasePath)
    {
        var directory = Path.GetDirectoryName(databasePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var options = new DbContextOptionsBuilder<PosDbContext>()
            .UseSqlite($"Data Source={databasePath}")
            .EnableSensitiveDataLogging()
            .Options;

        var context = new PosDbContext(options);
        await context.Database.EnsureCreatedAsync();
        return context;
    }

    private static string BuildTempPath(string scenario, string fileName)
    {
        return Path.Combine(Path.GetTempPath(), "POS.Tests", scenario, Guid.NewGuid().ToString("N"), fileName);
    }

    private static void CleanupPath(string filePath)
    {
        try
        {
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
            {
                return;
            }

            SqliteConnection.ClearAllPools();

            var rootDirectory = Directory.GetParent(directory)?.FullName;
            if (!string.IsNullOrWhiteSpace(rootDirectory) && Directory.Exists(rootDirectory))
            {
                Directory.Delete(rootDirectory, recursive: true);
            }
        }
        catch
        {
            // Keep cleanup best-effort to avoid masking test failures.
        }
    }

    private sealed class TestClock : ISystemClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }

    private sealed class NoOpCheckoutExecutionHook : ICheckoutExecutionHook
    {
        public Task OnAfterInventoryDeductionAsync(CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingCheckoutExecutionHook : ICheckoutExecutionHook
    {
        public Task OnAfterInventoryDeductionAsync(CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Simulated crash during checkout.");
        }
    }

    private sealed class AlwaysFailReceiptPrinterGateway : IReceiptPrinterGateway
    {
        public int Attempts { get; private set; }

        public Task PrintAsync(string receiptNumber, string payload, CancellationToken cancellationToken = default)
        {
            Attempts += 1;
            throw new IOException("Printer not connected.");
        }
    }

    private sealed class SuccessfulReceiptPrinterGateway : IReceiptPrinterGateway
    {
        public Task PrintAsync(string receiptNumber, string payload, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
