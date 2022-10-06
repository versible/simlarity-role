/***************************************************************************************************
/
/   This SQL is T-SQL written for SQL Server.  Hopefully it is not too difficult to translate
/   to other dialects of SQL as needed.
/
***************************************************************************************************/

-- Temporary tables
IF OBJECT_ID('tempdb..#CorrectedExpected') <> '' DROP TABLE #CorrectedExpected;
IF OBJECT_ID('tempdb..#SimRole')           <> '' DROP TABLE #SimRole;
IF OBJECT_ID('tempdb..#Limits')            <> '' DROP TABLE #Limits;

DECLARE @ScriptName VARCHAR(50) = 'SimRole Build Final Output';
DECLARE @LogMessage VARCHAR(1024);
DECLARE @MEMBER_LIMIT_FOR_SIMILARITY INT;
DECLARE @MEMBER_LIMIT_FOR_ROLE       INT;
DECLARE @BASKET_LIMIT_FOR_ROLE       INT;
DECLARE @TotalCustomers INT;
DECLARE @TotalBaskets INT;
DECLARE @TotalWeeks   INT;
DECLARE @TotalStores  INT;

-- These are important limits to prevent calculating Similarity and Role index for prod-prod
-- combinations with too little data to be reliable.
SET @MEMBER_LIMIT_FOR_SIMILARITY = 5; 
SET @MEMBER_LIMIT_FOR_ROLE       = 5; 
SET @BASKET_LIMIT_FOR_ROLE       = 10;

SET NOCOUNT ON;

SET @LogMessage = @ScriptName + 'STARTS'; PRINT( @LogMessage);

SELECT
      @TotalCustomers = TotalCustomers
    , @TotalBaskets   = TotalBaskets
    , @TotalWeeks     = TotalWeeks  
    , @TotalStores    = TotalStores 
FROM SimRole.Totals;
SET @LogMessage = @ScriptName + 'read in total metrics.'; PRINT( @LogMessage);

WITH cte AS (
    SELECT  
        pp.Prod1 AS ProdA
        ,pp.Prod2 AS ProdB

        ,ProdA_CustomerCount = p1.CustomerCount
        ,ProdA_BasketCount   = p1.BasketCount
        ,ProdA_WeekCount     = p1.WeekCount+1
        ,ProdA_StoreCount    = p1.StoreCount

        ,ProdB_CustomerCount = p2.CustomerCount
        ,ProdB_BasketCount   = p2.BasketCount
        ,ProdB_WeekCount     = p2.WeekCount+1
        ,ProdB_StoreCount    = p2.StoreCount

        ,CustomersBuyingBoth
        ,AnyBasketOfCustomersBuyingBoth
        ,BasketsContainingA_FromCustomersBuyingBoth
        ,BasketsContainingB_FromCustomersBuyingBoth
        ,BasketsContainingBoth
        ,TotalStoreCountBoth = TotalStoreCountBoth
        ,TotalWeekCountBoth  = TotalWeekCountBoth+1

        -- This is a correction factor for when a product pair does not have matched store distribution
        ,PredictedCustomersStoreFactor = TotalStoreCountBoth*1.0 / IIF(p1.StoreCount < p2.StoreCount, p1.StoreCount, p2.StoreCount)
        
        -- This is a correction factor for when a product pair does not have matched weeks-selling
        ,PredictedBasketsWeekFactor  = TotalWeekCountBoth *1.0 / IIF(p1.WeekCount  < p2.WeekCount , p1.WeekCount+1 , p2.WeekCount+1 )

        -- The key probability calculation for customers
        ,ExpectedCustomers = CONVERT(BIGINT,p1.CustomerCount) * CONVERT(BIGINT,p2.CustomerCount) * 1.0 / @TotalCustomers
        ,ObservedCustomers = pp.CustomersBuyingBoth

        -- The key probability calculation for baskets.
        -- NOTE this isn't calcualted on all baskets, but on the baskets of customers that buy both products (at some point during the year)
        ,ExpectedBastets = CONVERT(BIGINT,pp.BasketsContainingA_FromCustomersBuyingBoth) * CONVERT(BIGINT, pp.BasketsContainingB_FromCustomersBuyingBoth) * 1.0 / pp.AnyBasketOfCustomersBuyingBoth
        ,ObservedBaskets = pp.BasketsContainingBoth

    FROM SimRole.ProdProd pp
            INNER JOIN SimRole.Prod p1 ON pp.Prod1 = p1.Prod AND pp.ModelID = p1.ModelID
            INNER JOIN SimRole.Prod p2 ON pp.Prod2 = p2.Prod AND pp.ModelID = p2.ModelID
)
SELECT
      *
    , CorrectedExpectedCustomers = ExpectedCustomers * PredictedCustomersStoreFactor
    , CorrectedExpectedBaskets   = ExpectedBastets   * PredictedBasketsWeekFactor
INTO #CorrectedExpected
FROM cte;
SET @LogMessage = @ScriptName + 'has inserted rows in to #CorrectedExpected.  Rowcount = ' + LTRIM(STR(@@ROWCOUNT)); PRINT( @LogMessage);


-- We now compare out CorrectedExpected counts, and other fields, to the limits for reporting results
SELECT    
      *
    , CONVERT(BIT,IIF(ObservedCustomers          <=@MEMBER_LIMIT_FOR_SIMILARITY,1,0)) AS TooFewObservedCustomers
    , CONVERT(BIT,IIF(ExpectedCustomers          <=@MEMBER_LIMIT_FOR_SIMILARITY,1,0)) AS TooFewExpectedCustomers
    , CONVERT(BIT,IIF(CorrectedExpectedCustomers <=@MEMBER_LIMIT_FOR_SIMILARITY,1,0)) AS TooFewCorrectedExpectedCustomers
    , CONVERT(BIT,IIF(CustomersBuyingBoth        <=@MEMBER_LIMIT_FOR_ROLE      ,1,0)) AS TooFewCustomersBuyingBoth
    , CONVERT(BIT,IIF(ObservedBaskets            <=@BASKET_LIMIT_FOR_ROLE      ,1,0)) AS TooFewObservedBaskets
    , CONVERT(BIT,IIF(ExpectedBastets            <=@BASKET_LIMIT_FOR_ROLE      ,1,0)) AS TooFewExpectedBaskets
    , CONVERT(BIT,IIF(CorrectedExpectedBaskets   <=@BASKET_LIMIT_FOR_ROLE      ,1,0)) AS TooFewCorrectedExpectedBaskets
    , CONVERT(BIT,IIF(CorrectedExpectedCustomers = 0                           ,1,0)) AS ZeroCorrectedExpectedCustomers
    , CONVERT(BIT,IIF(CorrectedExpectedBaskets   = 0                           ,1,0)) AS ZeroCorrectedExpectedBaskets

INTO #Limits
FROM #CorrectedExpected;
SET @LogMessage = @ScriptName + 'has inserted rows in to #Limits.  Rowcount = ' + LTRIM(STR(@@ROWCOUNT)); PRINT( @LogMessage);


-- Time to create the final output.  If you have a product descriptions, now is the time join these in.
WITH cte AS (
    SELECT
          ProdA
        , ProdB

        , CONVERT(INT,
            CASE WHEN (TooFewObservedCustomers=1 AND TooFewCorrectedExpectedCustomers=1) THEN -1
                    WHEN ZeroCorrectedExpectedCustomers=1 THEN -2
                    ELSE ObservedCustomers / CorrectedExpectedCustomers * 100
            END
            ) AS SimilarityIndex
            
        , CONVERT(INT,
            CASE WHEN (TooFewCorrectedExpectedBaskets=1 AND TooFewObservedBaskets=1) THEN -1
                    WHEN ZeroCorrectedExpectedCustomers=1 THEN -2
                    WHEN ZeroCorrectedExpectedBaskets=1 THEN -3
                    WHEN TooFewCustomersBuyingBoth=1 THEN -4
                    ELSE ObservedBaskets / CorrectedExpectedBaskets * 100
                END
            ) AS RoleIndex

        , TotalStoreCountBoth
        , TotalWeekCountBoth 
    FROM #Limits
)
SELECT
      ProdA
    , ProdB
    , SimilarityIndex
    , RoleIndex
    , TotalStoreCountBoth
    , TotalWeekCountBoth 
INTO SimRole.FinalOutput
FROM cte
-- As Similarity Index and Role Index of ProdA-ProdB is the same as ProdB-ProdA, we have only computed ProdA-ProdB numbers.
-- In order to show all results in our final output table, we also need to store ProdB-ProdA.
-- Hence we have to "insert again" switching ProdA and ProdB values.
UNION
SELECT
      ProdA             = ProdB
    , ProdB             = ProdA
    , SimilarityIndex
    , RoleIndex
    , TotalStoreCountBoth
    , TotalWeekCountBoth 
INTO SimRole.FinalOutput
FROM cte
;
SET @LogMessage = @ScriptName + 'has inserted rows in to SimRole.FinalOutput.  Rowcount = ' + LTRIM(STR(@@ROWCOUNT)); PRINT( @LogMessage);


SET @LogMessage = @ScriptName + 'ENDS'; PRINT( @LogMessage);

SET NOCOUNT OFF;

-- SELECT * FROM SimRole.FinalOutput WHERE TotalStoreCountBoth > 500 and TotalWeekCountBoth > 50 ORDER BY SimilarityIndex DESC
