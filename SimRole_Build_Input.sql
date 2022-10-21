/**************************************************************************************************
/
/ This example code transforms data from TPC-DS datasets (https://www.tpc.org/tpcds/)
/
/ The TPC-DS datasets have an appropriate schema: Sales data with stores and products and customers...
/
/ However TPC-DS is designed for performance testing.  The data is therefore machine (randomly) generated.
/ Therefore the TPC-DS data is meaningless, so the outputs of running this model on this data will be meaningless.
/ We are providing example code based on TPC-DS data merely to ensure the code runs.
/
/ If anyone knows of a better open data source to use, please let us know!
/
**************************************************************************************************/

-- This code is writen for PostgreSQL

COPY (
        
         WITH Sales AS (
                SELECT
                          s.ss_store_sk      AS StoreID
                        , s.ss_item_sk       AS ProductID
                        , s.ss_customer_sk   AS CustomerID
                        , s.ss_ticket_number AS BasketID
                        , d.d_week_seq       AS WeekID
                FROM dev_raw.store_sales s
                  INNER JOIN dev_raw.date_dim d ON s.ss_sold_date_sk = d.d_date_sk
        )
        , StartWeek AS (
                SELECT MIN(WeekID)+1 AS WeekID FROM Sales
        )
        , EndWeek AS (
                SELECT WeekID+51 AS WeekID FROM StartWeek
        )
        , BasketCustomer AS (
                -- CustomerID can be NULL.  This cte ensures we always have a non-null value for each BasketID
                SELECT 
                          BasketID
                        , MAX(CustomerID) AS CustomerID
                FROM Sales
                WHERE CustomerID IS NOT NULL
                GROUP BY BasketID
        )        
        , BasketStore AS (
                -- StoreID can be NULL.  This cte ensures we always have a non-null value for each BasketID
                SELECT 
                          BasketID
                        , MAX(StoreID) AS StoreID
                FROM Sales
                WHERE StoreID IS NOT NULL
                GROUP BY BasketID
        )        
        
        SELECT 
                  bs.StoreID
                , cte.ProductID
                , bc.CustomerID
                , cte.BasketID
                , cte.WeekID 
        
        FROM cte cte
            INNER JOIN BasketCustomer bc ON cte.BasketID = bc.BasketID
            INNER JOIN BasketStore    bs ON cte.BasketID = bs.BasketID
        
        WHERE cte.WeekID BETWEEN (SELECT WeekID FROM StartWeek) AND (SELECT WeekID FROM EndWeek)
        
        ORDER BY 
                  bc.CustomerID ASC
                , cte.BasketID ASC
                , cte.ProductID ASC
)

TO 'C:\Program Files\PostgreSQL\14\data\SimRoleInput.tab'  -- Or other location with write permission

WITH (FORMAT 'csv'
    , DELIMITER E'\t'
    , HEADER
);