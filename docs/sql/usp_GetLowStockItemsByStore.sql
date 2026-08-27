USE [fis];
GO

CREATE OR ALTER PROCEDURE dbo.usp_GetLowStockItemsByStore
    @StoreCode INT
AS
BEGIN
    SET NOCOUNT ON;

    IF @StoreCode IS NULL OR @StoreCode <= 0
    BEGIN
        THROW 50001, 'A valid @StoreCode is required.', 1;
    END;

    DECLARE @MinVerificationStatus INT =
        CASE
            WHEN @StoreCode IN (3, 6) THEN 0
            ELSE 1
        END;

    ;WITH BaseItems AS
    (
        SELECT
            cod.ItemDescriptionCode AS ItemCode,
            cod.CategoryName,
            cod.ItemDescription AS ItemName,
            ci.ItemName AS MainCategory,
            cod.UnitShortName AS Unit,
            cod.ItemType
        FROM dbo.COIDescription AS cod
        INNER JOIN dbo.ChartofItems AS ci
            ON ci.ItemCode = cod.ParentID
        WHERE cod.Status = 'Active'
    ),
    GoodsReceiptAgg AS
    (
        SELECT
            grn.ItemCode,
            SUM(ISNULL(grn.ReceivedQty, 0)) AS ReceivedQty
        FROM dbo.GoodsReceiptNoteDetail AS grn
        WHERE grn.StoreCode = @StoreCode
        GROUP BY grn.ItemCode
    ),
    TransferInRegularAgg AS
    (
        SELECT
            stn.ItemCode,
            SUM(ISNULL(stn.Qty, 0)) AS TransferQty
        FROM dbo.StockTransferNoteDetail AS stn
        INNER JOIN dbo.StockTransferNoteMaster AS stm
            ON stm.FormNo = stn.FormNo
        WHERE stn.ToStoreCode = @StoreCode
          AND CONVERT(INT, stn.VerificationStatus) >= @MinVerificationStatus
          AND stm.Type IN (0, 3)
        GROUP BY stn.ItemCode
    ),
    TransferInCapexStore2Agg AS
    (
        SELECT
            stn.ItemCode,
            SUM(ISNULL(stn.Qty, 0)) AS TransferQty
        FROM dbo.StockTransferNoteDetail AS stn
        INNER JOIN dbo.StockTransferNoteMaster AS stm
            ON stm.FormNo = stn.FormNo
        WHERE @StoreCode = 2
          AND stn.ToStoreCode =
              CASE
                  WHEN stm.Purpose = 'Stock Transfer Return' THEN 2
                  ELSE 7
              END
          AND CONVERT(INT, stn.VerificationStatus) >= @MinVerificationStatus
          AND stm.Type = 1
        GROUP BY stn.ItemCode
    ),
    MiscInAgg AS
    (
        SELECT
            ms.ItemCode,
            SUM(ISNULL(ms.Quantity, 0)) AS MiscInQty
        FROM dbo.MiscStockDetail AS ms
        INNER JOIN dbo.MiscStockMaster AS msm
            ON msm.FormNo = ms.FormNo
        WHERE ms.QuantityInOut = 'I'
          AND ms.StoreCode = @StoreCode
          AND msm.ApprovedStatus = 'Approved'
        GROUP BY ms.ItemCode
    ),
    StockReturnAgg AS
    (
        SELECT
            srd.ItemCode,
            SUM(ISNULL(srd.ReturnQty, 0)) AS ReturnQty
        FROM dbo.StockReturnDetail AS srd
        WHERE srd.StoreCode = @StoreCode
        GROUP BY srd.ItemCode
    ),
    TransferOutAgg AS
    (
        SELECT
            stn.ItemCode,
            SUM(ISNULL(stn.Qty, 0)) AS TransferQty
        FROM dbo.StockTransferNoteDetail AS stn
        WHERE stn.FromStoreCode = @StoreCode
        GROUP BY stn.ItemCode
    ),
    MiscOutAgg AS
    (
        SELECT
            ms.ItemCode,
            SUM(ISNULL(ms.Quantity, 0)) AS MiscOutQty
        FROM dbo.MiscStockDetail AS ms
        INNER JOIN dbo.MiscStockMaster AS msm
            ON msm.FormNo = ms.FormNo
        WHERE ms.QuantityInOut = 'O'
          AND ms.StoreCode = @StoreCode
          AND msm.ApprovedStatus = 'Approved'
        GROUP BY ms.ItemCode
    ),
    IssueAgg AS
    (
        SELECT
            si.ItemCode,
            SUM(ISNULL(si.IssuedQty, 0)) AS IssuedQty
        FROM dbo.StoreIssueNoteDetail AS si
        WHERE si.StoreCode = @StoreCode
        GROUP BY si.ItemCode
    ),
    OgpAgg AS
    (
        SELECT
            ogp.ItemCode,
            SUM(ISNULL(ogp.Qty, 0)) AS OgpQty
        FROM dbo.GeneralOGPDetail AS ogp
        WHERE ogp.StoreCode = @StoreCode
        GROUP BY ogp.ItemCode
    ),
    StockBase AS
    (
        SELECT
            bi.ItemCode,
            bi.CategoryName,
            bi.ItemName,
            bi.MainCategory,
            bi.Unit,
            NetStockRaw =
                COALESCE(gr.ReceivedQty, 0)
                + CASE
                      WHEN bi.ItemType = 'Capex' AND @StoreCode = 2
                          THEN COALESCE(tic.TransferQty, 0)
                      ELSE COALESCE(tir.TransferQty, 0)
                  END
                + COALESCE(mi.MiscInQty, 0)
                + COALESCE(sr.ReturnQty, 0)
                - COALESCE(toa.TransferQty, 0)
                - COALESCE(mo.MiscOutQty, 0)
                - COALESCE(ia.IssuedQty, 0)
                - COALESCE(oa.OgpQty, 0)
        FROM BaseItems AS bi
        LEFT JOIN GoodsReceiptAgg AS gr
            ON gr.ItemCode = bi.ItemCode
        LEFT JOIN TransferInRegularAgg AS tir
            ON tir.ItemCode = bi.ItemCode
        LEFT JOIN TransferInCapexStore2Agg AS tic
            ON tic.ItemCode = bi.ItemCode
        LEFT JOIN MiscInAgg AS mi
            ON mi.ItemCode = bi.ItemCode
        LEFT JOIN StockReturnAgg AS sr
            ON sr.ItemCode = bi.ItemCode
        LEFT JOIN TransferOutAgg AS toa
            ON toa.ItemCode = bi.ItemCode
        LEFT JOIN MiscOutAgg AS mo
            ON mo.ItemCode = bi.ItemCode
        LEFT JOIN IssueAgg AS ia
            ON ia.ItemCode = bi.ItemCode
        LEFT JOIN OgpAgg AS oa
            ON oa.ItemCode = bi.ItemCode
    ),
    RoundedStock AS
    (
        SELECT
            sb.ItemCode,
            sb.CategoryName,
            sb.ItemName,
            sb.MainCategory,
            sb.Unit,
            sb.NetStockRaw,
            ROUND(sb.NetStockRaw, 4) AS Stock
        FROM StockBase AS sb
    ),
    FilteredRows AS
    (
        SELECT
            rs.ItemCode,
            rs.CategoryName,
            rs.ItemName,
            rs.MainCategory,
            rs.Unit,
            rs.NetStockRaw,
            rs.Stock
        FROM RoundedStock AS rs
        WHERE rs.Stock < 1
          AND rs.Stock <> 0
    ),
    LocationQtyAgg AS
    (
        SELECT
            ml.ItemCode,
            SUM(ISNULL(ml.Qty, 0)) AS LocationQty
        FROM dbo.MasterLocations AS ml
        INNER JOIN FilteredRows AS fr
            ON fr.ItemCode = ml.ItemCode
        WHERE ml.StoreCode = @StoreCode
          AND ml.IsIssued = 0
          AND ml.Status = 'Active'
          AND ml.InOut = 'IN'
        GROUP BY ml.ItemCode
    ),
    DistinctLocationBase AS
    (
        SELECT DISTINCT
            ml.ItemCode,
            NULLIF(LTRIM(RTRIM(dl.LocationNumber + ' ' + dl.Shelf + ' ' + dl.Bin)), '') AS LocationLabel
        FROM dbo.MasterLocations AS ml
        INNER JOIN FilteredRows AS fr
            ON fr.ItemCode = ml.ItemCode
        INNER JOIN dbo.DepartmentLocation AS dl
            ON CONVERT(NVARCHAR(500), dl.LocationCode) = CONVERT(NVARCHAR(500), ml.LocationCode)
        WHERE ml.StoreCode = @StoreCode
          AND ml.IsIssued = 0
          AND ml.Status = 'Active'
          AND ml.InOut = 'IN'
    ),
    LocationTextAgg AS
    (
        SELECT
            d.ItemCode,
            STUFF
            (
                (
                    SELECT ', ' + d2.LocationLabel
                    FROM DistinctLocationBase AS d2
                    WHERE d2.ItemCode = d.ItemCode
                      AND d2.LocationLabel IS NOT NULL
                    FOR XML PATH(''), TYPE
                ).value('.', 'NVARCHAR(MAX)'),
                1,
                2,
                ''
            ) AS Location
        FROM (SELECT DISTINCT ItemCode FROM DistinctLocationBase) AS d
    )
    SELECT
        fr.ItemCode,
        fr.CategoryName,
        fr.ItemName,
        CAST(0 AS DECIMAL(18, 4)) AS QtyIn,
        CAST(0 AS DECIMAL(18, 4)) AS QtyOut,
        COALESCE(lt.Location, '') AS Location,
        fr.MainCategory,
        fr.Unit,
        fr.Stock,
        ROUND(COALESCE(lq.LocationQty, 0), 2) AS LocationWise,
        ROUND(fr.NetStockRaw, 0) - ROUND(COALESCE(lq.LocationQty, 0), 0) AS Differences
    FROM FilteredRows AS fr
    LEFT JOIN LocationQtyAgg AS lq
        ON lq.ItemCode = fr.ItemCode
    LEFT JOIN LocationTextAgg AS lt
        ON lt.ItemCode = fr.ItemCode
    ORDER BY fr.ItemName;
END;
GO
