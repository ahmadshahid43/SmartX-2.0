param(
  [string[]]$Targets = @(
    "src/OmniBusiness.Api/Data/foundation.json"
  )
)

$ErrorActionPreference = "Stop"
$RuntimeTarget = ".artifacts/runtime/foundation.local.json"

$tenantId = "11111111-1111-1111-1111-111111111111"
$branchId = "44444444-4444-4444-4444-444444444441"
$now = [DateTimeOffset]::Now

function New-Product($id, $sku, $name, $category, $price, $inHand, $reserved, $reorder, $code) {
  [ordered]@{ id=$id; tenantId=$tenantId; sku=$sku; name=$name; category=$category; unitPrice=$price; inHand=$inHand; reserved=$reserved; warehouse="Main Warehouse"; status=if ($inHand - $reserved -le $reorder) { "Low Stock" } else { "In Stock" }; isFavorite=$false; isQuickSale=$true; isLowStock=($inHand - $reserved -le $reorder); visualCode=$code; reorderLevel=$reorder; isArchived=$false }
}
function New-Line($product, $quantity) {
  [ordered]@{ productId=$product.id; sku=$product.sku; name=$product.name; quantity=$quantity; unitPrice=$product.unitPrice; lineTotal=($product.unitPrice * $quantity) }
}

$products = @(
  (New-Product "10000000-0000-0000-0000-000000000001" "GRC-MLK-001" "Olpers UHT Full Cream Milk 1L" "Dairy & Eggs" 375 48 2 12 "OLP"),
  (New-Product "10000000-0000-0000-0000-000000000002" "GRC-TEA-430" "Tapal Danedar Tea 430g" "Tea & Coffee" 945 20 1 8 "TAP"),
  (New-Product "10000000-0000-0000-0000-000000000003" "GRC-OIL-001" "Kausar Cooking Oil 1L" "Cooking Oil & Ghee" 550 35 0 10 "KAU"),
  (New-Product "10000000-0000-0000-0000-000000000004" "GRC-RICE-001" "Guard Supreme Basmati Rice 1kg" "Rice & Flour" 520 24 1 8 "GRD"),
  (New-Product "10000000-0000-0000-0000-000000000005" "GRC-SUG-002" "Local Desi Sugar 2kg" "Staples" 310 60 4 15 "SUG"),
  (New-Product "10000000-0000-0000-0000-000000000006" "GRC-WTR-005" "Nestle Pure Life Water 5L" "Beverages" 315 14 1 10 "NPL"),
  (New-Product "10000000-0000-0000-0000-000000000007" "GRC-CHP-001" "Lays Classic Salted 50g" "Snacks" 78 85 5 20 "LAY"),
  (New-Product "10000000-0000-0000-0000-000000000008" "GRC-KTC-800" "National Tomato Ketchup 800g" "Sauces & Spices" 345 7 0 8 "NAT"),
  (New-Product "10000000-0000-0000-0000-000000000009" "GRC-TIS-550" "Rose Petal Smart Pack 550 Sheets" "Home Care" 415 11 1 10 "RPT"),
  (New-Product "10000000-0000-0000-0000-000000000010" "GRC-EGG-030" "Farm Fresh Classic Eggs 30 Pack" "Dairy & Eggs" 695 9 0 12 "EGG"),
  (New-Product "10000000-0000-0000-0000-000000000011" "GRC-DAL-500" "Daal Moong Wash 500g" "Pulses & Spices" 220 32 2 10 "DAL"),
  (New-Product "10000000-0000-0000-0000-000000000012" "GRC-FRZ-001" "K&N's Tender Pops 780g" "Frozen Food" 1260 6 0 8 "KNN"),
  (New-Product "10000000-0000-0000-0000-000000000013" "FRS-POT-001" "Fresh Potato 1kg" "Fresh Produce" 59 42 0 15 "POT"),
  (New-Product "10000000-0000-0000-0000-000000000014" "FRS-TOM-001" "Fresh Tomato 1kg" "Fresh Produce" 419 8 0 10 "TOM"),
  (New-Product "10000000-0000-0000-0000-000000000015" "GRC-SOA-004" "Sufi Laundry Soap 4 Pack" "Home Care" 545 18 1 10 "SUF")
)

$sales = @(
  @{ ref="INV-0001"; customer="Ayesha Khan"; items=@((New-Line ($products[0]) 2),(New-Line ($products[1]) 1),(New-Line ($products[6]) 3)); method="Cash"; days=0; tax=344; discount=50; fbr="Reported" },
  @{ ref="INV-0002"; customer="Usman Ali"; items=@((New-Line ($products[3]) 2),(New-Line ($products[4]) 2),(New-Line ($products[2]) 1)); method="Card"; days=1; tax=404; discount=0; fbr="Reported" },
  @{ ref="INV-0003"; customer="Sara Ahmed"; items=@((New-Line ($products[9]) 1),(New-Line ($products[11]) 1),(New-Line ($products[8]) 2)); method="Digital Wallet"; days=2; tax=404; discount=75; fbr="QueuedOffline" },
  @{ ref="INV-0004"; customer="Walk-in Customer"; items=@((New-Line ($products[12]) 4),(New-Line ($products[13]) 2),(New-Line ($products[10]) 2)); method="Cash"; days=3; tax=156; discount=0; fbr="Reported" }
) | ForEach-Object {
  $items = @($_.items)
  $subtotal = [decimal](($items | ForEach-Object { $_.lineTotal } | Measure-Object -Sum).Sum)
  $itemCount = [int](($items | ForEach-Object { $_.quantity } | Measure-Object -Sum).Sum)
  $amount = $subtotal - $_.discount + $_.tax
  [ordered]@{ id=[guid]::NewGuid().ToString(); tenantId=$tenantId; referenceNo=$_.ref; customerName=$_.customer; amount=$amount; grossProfit=[math]::Round($subtotal * .14, 0); status="Completed"; occurredAt=$now.AddDays(-1 * $_.days).ToString("o"); itemCount=$itemCount; discount=$_.discount; tax=$_.tax; paymentMethod=$_.method; cashierName="Ahmad"; lines=$items; receivedAmount=$amount; changeAmount=0; fbrStatus=$_.fbr; fbrInvoiceNumber=if ($_.fbr -eq "Reported") { "FBR-$($_.ref)" } else { $null }; paidAmount=$amount; balanceAmount=0; paymentStatus="Paid"; payments=@(@{ method=$_.method; amount=$amount; referenceNo=$null }); refundedAmount=0; refundedAt=$null; refundedBy=$null; refundReason=$null; inventoryReturned=$false }
}

foreach ($target in $Targets) {
  if (-not (Test-Path $target)) { continue }
  $data = Get-Content -LiteralPath $target -Raw | ConvertFrom-Json -AsHashtable
  $data['company']['name'] = "SmartX Grocery Demo - Lahore"
  $data['tenant']['name'] = "SmartX Grocery Demo"
  $data['products'] = $products
  $data['customers'] = @(
    @{ id="20000000-0000-0000-0000-000000000001"; tenantId=$tenantId; name="Walk-in Customer"; pricingTier="Retail Pricing"; avatarLetter="W"; phoneNumber=$null; isWalkIn=$true; email=$null; loyaltyTier="Standard"; loyaltyPoints=0; storeCreditBalance=0; giftCardBalance=0; marketingOptIn=$false; lastVisitAt=$now.ToString("o") },
    @{ id="20000000-0000-0000-0000-000000000002"; tenantId=$tenantId; name="Ayesha Khan"; pricingTier="Retail Pricing"; avatarLetter="A"; phoneNumber="03001234567"; isWalkIn=$false; email="ayesha.demo@example.com"; loyaltyTier="Gold"; loyaltyPoints=240; storeCreditBalance=150; giftCardBalance=0; marketingOptIn=$true; lastVisitAt=$now.ToString("o") },
    @{ id="20000000-0000-0000-0000-000000000003"; tenantId=$tenantId; name="Usman Ali"; pricingTier="Retail Pricing"; avatarLetter="U"; phoneNumber="03011234567"; isWalkIn=$false; email="usman.demo@example.com"; loyaltyTier="Standard"; loyaltyPoints=80; storeCreditBalance=0; giftCardBalance=500; marketingOptIn=$true; lastVisitAt=$now.AddDays(-1).ToString("o") },
    @{ id="20000000-0000-0000-0000-000000000004"; tenantId=$tenantId; name="Sara Ahmed"; pricingTier="Wholesale Pricing"; avatarLetter="S"; phoneNumber="03021234567"; isWalkIn=$false; email="sara.demo@example.com"; loyaltyTier="Silver"; loyaltyPoints=120; storeCreditBalance=0; giftCardBalance=0; marketingOptIn=$false; lastVisitAt=$now.AddDays(-2).ToString("o") }
  )
  $data['vendors'] = @(
    @{ id="30000000-0000-0000-0000-000000000001"; tenantId=$tenantId; name="Nestle Pakistan Distribution"; contactPerson="Bilal Raza"; phoneNumber="042-111-637-853"; city="Lahore"; leadTimeLabel="2 days"; paymentTerms="Net 15"; status="Active" },
    @{ id="30000000-0000-0000-0000-000000000002"; tenantId=$tenantId; name="Metro Cash & Carry Wholesale"; contactPerson="Saad Iqbal"; phoneNumber="042-111-786-786"; city="Lahore"; leadTimeLabel="Same week"; paymentTerms="Cash on delivery"; status="Active" }
  )
  $data['recentTransactions'] = $sales
  $data['dailyFigures'] = @(@{ date=$now.Date.ToString("yyyy-MM-dd"); sales=5632; purchases=4800; grossProfit=788 },@{ date=$now.AddDays(-1).Date.ToString("yyyy-MM-dd"); sales=4200; purchases=3500; grossProfit=700 })
  $data['salesTrend'] = @(@{ label="Mon"; value=4200 },@{ label="Tue"; value=5632 },@{ label="Wed"; value=4850 },@{ label="Thu"; value=6100 },@{ label="Fri"; value=5300 })
  $data['topSelling'] = @(@{ name="Olpers UHT Full Cream Milk 1L"; units=28; revenue=10500 },@{ name="Local Desi Sugar 2kg"; units=19; revenue=5890 },@{ name="Lays Classic Salted 50g"; units=42; revenue=3276 })
  $data['branchPerformance'] = @(@{ branchName="Main Branch"; percentage=100 })
  $data['stockAdjustments'] = @(@{ id=[guid]::NewGuid().ToString(); tenantId=$tenantId; productId=$products[13].id; productName=$products[13].name; quantityDelta=-3; reason="Fresh produce spoilage"; performedBy="Admin"; occurredAt=$now.AddDays(-1).ToString("o") })
  $data['purchaseOrders'] = @(@{ id=[guid]::NewGuid().ToString(); tenantId=$tenantId; vendorId="30000000-0000-0000-0000-000000000001"; purchaseOrderNo="PO-GRC-1001"; vendorName="Nestle Pakistan Distribution"; status="Open"; createdAt=$now.AddDays(-1).ToString("o"); expectedAt=$now.AddDays(2).ToString("o"); totalAmount=48750; lineCount=12; orderedUnits=180; receivedUnits=0 })
  $data['stockTransfers'] = @(@{ id=[guid]::NewGuid().ToString(); tenantId=$tenantId; transferNo="ST-GRC-1001"; fromBranchName="Main Branch"; toBranchName="Gulberg Outlet"; status="In Transit"; createdAt=$now.ToString("o"); expectedAt=$now.AddDays(1).ToString("o"); units=24; requestedBy="Admin"; notes="Milk and staples replenishment" })
  $data['cashShifts'] = @(@{ id=[guid]::NewGuid().ToString(); tenantId=$tenantId; userId="16d3cb0e-15be-4114-a7be-beb22a3d96a6"; cashierName="Ahmad"; registerName="Counter 01"; openedAt=$now.AddHours(-5).ToString("o"); closedAt=$null; openingFloat=5000; cashSales=2400; refunds=0; paidOuts=0; expectedCash=7400; countedCash=7400; status="Open" })
  $data['nextSaleSequence'] = 9001
  $data | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $target -Encoding utf8
}

# The API uses this separate file as its writable local/offline store. Replace it
# from the known-good seed snapshot so both development modes start identically.
if ($Targets.Count -eq 1 -and (Test-Path $Targets[0])) {
  Copy-Item -LiteralPath $Targets[0] -Destination $RuntimeTarget -Force
}

Write-Host "Pakistan grocery demo data seeded."
