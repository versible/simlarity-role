﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Data;
using System.Data.SqlClient;
using System.Linq;

namespace SimRole
{

    // Data structures (enumerations) to use in place of meaningless numbers in array indexes
    enum ProdProdMetrics
    {
        CustomersBuyingBoth,
        AnyBasketOfCustomersBuyingBoth,
        BasketsContainingA_FromCustomersBuyingBoth,
        BasketsContainingB_FromCustomersBuyingBoth,
        BasketsContainingBoth,
        TotalStoreCountBoth,
        TotalWeekCountBoth
    };
    enum ProdMetrics
    {
        CustomerCount,
        BasketCount,
        WeekCount,
        StoreCount
    };

     class Program
     {

        // CONSTANTS 

        // Controls how frequently a progress message is printed to the console
        private const long FILE_READ_MESSAGE_ROWS = 1000000;
        private const long PRODPROD_PROCESS_MESSAGE_ROWS = 50000;

        // The limit on the number of products the model can process.  This is important
        // as it sets the size of the arrays used during processing, which in turn controls
        // how much memory the application will consume.
        private const int NUM_PRODS = 350;  // Make 1 bigger than necessary to cope with zero-indexed arrays

        // The number of metrics in the ProdProd array
        // Must be the same as the lengh of the enum ProdProdMetrics
        private const int PRODPRODMETRICSIZE = 7;

        // The number of metrics in the  Product array
        // Must be the same as the lengh of the enum ProdMetrics
        private const int PRODMETRICSIZE = 4;

        // Following constants used to define the size of the arrays used during processing
        private const int NUM_WEEKS = 53;  // Make 1 bigger than necessary to cope with zero-indexed arrays
        private const int NUM_STORES = 1650;  // Make 1 bigger than necessary to cope with zero-indexed arrays

        // Main data input file
        // The input data has to be a text file (as opposed to a direct database connection) for performance reasons
        // 1. A text file read can stream - processing data on-the-fly so keeping memory consumption down
        // 2. Data type conversion is much more efficient with a text file read compared to a direct database connection
        private const string FILEIN = "C:\\temp\\SimRoleInput.tab";
        // This text file MUST contain sorted data created by a SQL statement like follows:
        //   SELECT StoreID, ProductID, CustomerID, BasketID, WeekID 
        //   FROM Your_Source_Set
        //   ORDER BY CustomerID ASC, BasketID ASC, ProductID ASC;
        //
        //  This code also expects a tab field delimiter (easily changed)
        

        // WORKING DATA STRUCTURES

        // 3 dimensional array.  Picture as a 2d array of size NUM_PRODS x NUM_PRODS
        // with each 2d cell holding PRODPRODMETRICSIZE different metrics
        private static int[,,] ProdProdMetric = new int[NUM_PRODS, NUM_PRODS, PRODPRODMETRICSIZE];

        // Similar to above, but for metrics that belong to products rather than product pairs
        private static int[,] ProdMetric = new int[NUM_PRODS, PRODMETRICSIZE];

        // For each customer, tally of how many of which product the customer bought
        private static int[] ProdCount_ThisCustomer = new int[NUM_PRODS];

        // 2d arrays of true/false as to if the combination has been observed in the input data
        private static bool[,]  ProdWeek = new bool[NUM_PRODS, NUM_WEEKS];  // defaults to all values false
        private static bool[,]  ProdStore = new bool[NUM_PRODS, NUM_STORES];  // defaults to all values false


        // WORKING VARIABLES

        private static int  NumBasketsForCustomer;
        private static long TotalCustomers = 0;
        private static long TotalBaskets = 0;
        private static long TotalWeeks = 0;
        private static long TotalStores = 0;

        public static void Main(string[] args)
        {

            // The main method of the application.  When run, execute starts here.

            // Set up some timers to show how long steps take to run
            DateTime start = DateTime.Now;
            TimeSpan t;

            // This is the BIG step.  Read and process the input file to the class data structures
            ReadFile();

            t = DateTime.Now - start;
            System.Console.WriteLine("Read in and process took " + t.TotalSeconds.ToString("0.0") + " secs");
            start = DateTime.Now;

            // Now process the class data structures
            PopulateTotalCounts();
            PopulateProdCounts();
            PopulateProdProdBothCounts();

            t = DateTime.Now - start;
            System.Console.WriteLine("Computing counts took " + t.TotalSeconds.ToString("0.0") + " secs");
            start = DateTime.Now;

            // Create output
            WriteOut();

            t = DateTime.Now - start;
            System.Console.WriteLine("Writing output took " + t.TotalSeconds.ToString("0.0") + " secs");

            System.Console.WriteLine("Press any key to end ...");
            System.Console.ReadKey();
        }

        private static void ReadFile()
        {

            // Data structures to hold the "list" of products for the basket or customer being processed.
            // These "lists" should not contain duplicates.
            // Because these "lists" are small compared to the list of all products, HashSet performs better than an array
            // in getting to an unduplicated set of products purchased in a basket or by a customer
            HashSet<int> BasketProdSet = new HashSet<int>();  
            HashSet<int> CustomerProdSet = new HashSet<int>();

            // Variables to hold the value of the last basket and customer processed
            // Used to identify when a new basket / customer is reached
            // *** This is why it is essential the input data file is sorted ***
            int LastBasket = 0;
            int LastCustomer = 0;

            string line;
            long i = 1;
            int Store;
            int Product;
            int Customer;
            int Basket;
            int Week;

            System.IO.StreamReader file = new System.IO.StreamReader(FILEIN);

            // Read the file header, do nothing with it, then read the first data row
            line = file.ReadLine();
            System.Console.WriteLine("Processing input data ...");
            line = file.ReadLine();

            // Loop through lines of the file until the end of file
            while (line != null)
            {

                // Split the line into parts
                string[] line_elements = line.Split('\t');
               
                Store  = Convert.ToInt32(line_elements[0]);
                Product = Convert.ToInt32(line_elements[1]);
                Customer = Convert.ToInt32(line_elements[2]);
                Basket = Convert.ToInt32(line_elements[3]);
                Week   = Convert.ToInt32(line_elements[4]);

                // Record the Product-week and Product-store combination.
                // It doesn't matter if we set values to true that were already true.
                // Doing this will be more efficient than testing for false then setting to true
                ProdWeek[Product, Week] = true;
                ProdStore[Product, Store] = true;

                // Write message to console to inform about progress
                if ((i % FILE_READ_MESSAGE_ROWS) == 0) System.Console.WriteLine("Reading data: " + i);
                
                if (LastBasket != Basket)
                {
                    // We have a new basket ... process the old one
                    ProcessBasket(BasketProdSet);

                    // Clear the basket set ready for the next one
                    BasketProdSet.Clear();
                }
                if (LastCustomer != Customer)
                {
                    // We have a new customer ... process the old one
                    ProcessCustomer(CustomerProdSet);

                    // Clear the customer set ready for the next one
                    CustomerProdSet.Clear();
                }

                // Add this Product to both the basket and customer sets
                BasketProdSet.Add(Product);
                CustomerProdSet.Add(Product);

                LastBasket = Basket;
                LastCustomer = Customer;

                i++;
                line = file.ReadLine();

            }

            file.Close();

            // Process last basket and last customer in input data
            ProcessBasket(BasketProdSet);
            ProcessCustomer(CustomerProdSet);

            System.Console.WriteLine("Process input data complete.");
            
        }

        private static void PopulateTotalCounts()
        {

            // Work out total weeks and total stores
            // This could probably be hard coded, but is really quick to compute.

            bool ThisProd = false;

            // Week Count
            // Use the ProdWeek boolean array
            // If any Product sold in a week (true in array), count the week
            for (int k = 0; k < NUM_WEEKS; k++)
            {
                ThisProd = false;
                for (int e1 = 0; e1 < NUM_PRODS; e1++)
                {
                    if (ProdWeek[e1, k] == true) ThisProd = true;
                }
                if (ThisProd) TotalWeeks++;
            }

            // Store Count
            // Use the ProdStore boolean array
            // If any Product sold in a store (true in array), count the store
            for (int k = 0; k < NUM_STORES; k++)
            {
                ThisProd = false;
                for (int e1 = 0; e1 < NUM_PRODS; e1++)
                {
                    if (ProdStore[e1, k] == true) ThisProd = true;
                }
                if (ThisProd) TotalStores++;
            }
        }

        private static void PopulateProdCounts()
        {

            // Work out week counts and store counts for each product

            int wc;  // week count
            int sc; // store count

            for (int e1 = 0; e1 < NUM_PRODS; e1++)
            {
                // Week Count
                // Use the ProdWeek boolean array
                // If any Product sold in a week (true in array), count the week for that Product
                wc = 0;
                for (int k = 0; k < NUM_WEEKS; k++)
                {
                    if (ProdWeek[e1, k] == true) wc++;
                }
                ProdMetric[e1, (int)ProdMetrics.WeekCount] = wc;

                // Store Count
                // Use the ProdStore boolean array
                // If any Product sold in a store (true in array), count the store for that Product
                sc = 0;
                for (int k = 0; k < NUM_STORES; k++)
                {
                    if (ProdStore[e1, k] == true) sc++;
                }
                ProdMetric[e1, (int)ProdMetrics.StoreCount] = sc;
            }
        }

        private static void PopulateProdProdBothCounts()
        {

            // Work out week counts and store counts for each Prod-Prod combination

            long c = 0; // counter used to logging message to console
            int wc;  // week count
            int sc; // store count

            // Find all combinations of products
            for (int e1 = 0; e1 < NUM_PRODS; e1++)
            {
                // I think this could probably start with e2 = e1 given we only consider
                // e2 > e1 in ProcessCustomer and ProcessBasket
                for (int e2 = 0; e2 < NUM_PRODS; e2++)
                {

                    // Protect against ProdProd combinations without a customer
                    // (Maybe this is because of the e2 > e1 point above)
                    if (ProdProdMetric[e1,e2,(int)ProdProdMetrics.CustomersBuyingBoth] > 0)
                    {
                        // Week Count
                        // Use the ProdWeek boolean array
                        // If any Product-Product sold in a week (true in array), count the week for that Product
                        wc = 0;
                        for (int k = 0; k < NUM_WEEKS; k++)
                        {
                            if ((ProdWeek[e1, k] == true) & (ProdWeek[e2, k] == true)) wc++;
                        }
                        ProdProdMetric[e1, e2, (int)ProdProdMetrics.TotalWeekCountBoth] = wc;

                        // Store Count
                        // Use the ProdStore boolean array
                        // If any Product-Product sold in a store (true in array), count the store for that Product
                        sc = 0;
                        for (int k = 0; k < NUM_STORES; k++)
                        {
                            if ((ProdStore[e1, k] == true) & (ProdStore[e2, k] == true)) sc++;
                        }
                        ProdProdMetric[e1, e2, (int)ProdProdMetrics.TotalStoreCountBoth] = sc;

                        // Do some logging as this step takes time!
                        if ((c+1 % ProdProd_PROCESS_MESSAGE_ROWS) == 0) System.Console.WriteLine("ProdProd Count Both: " + c);
                        c++;
                    }
                }
            }
        }


        private static DataTable MakeProdProdTable()
        {
            DataTable ProdProd = new DataTable("ProdProd");

            ProdProd.Columns.Add(new DataColumn("ModelID",                                    System.Type.GetType("System.Int32")));
            ProdProd.Columns.Add(new DataColumn("ModelDesc",                                  System.Type.GetType("System.String")));
            ProdProd.Columns.Add(new DataColumn("Prod1",                                      System.Type.GetType("System.Int32")));
            ProdProd.Columns.Add(new DataColumn("Prod2",                                      System.Type.GetType("System.Int32")));
            ProdProd.Columns.Add(new DataColumn("CustomersBuyingBoth",                        System.Type.GetType("System.Int32")));
            ProdProd.Columns.Add(new DataColumn("AnyBasketOfCustomersBuyingBoth",             System.Type.GetType("System.Int32")));
            ProdProd.Columns.Add(new DataColumn("BasketsContainingA_FromCustomersBuyingBoth", System.Type.GetType("System.Int32")));
            ProdProd.Columns.Add(new DataColumn("BasketsContainingB_FromCustomersBuyingBoth", System.Type.GetType("System.Int32")));
            ProdProd.Columns.Add(new DataColumn("BasketsContainingBoth",                      System.Type.GetType("System.Int32")));
            ProdProd.Columns.Add(new DataColumn("TotalStoreCountBoth",                        System.Type.GetType("System.Int32")));
            ProdProd.Columns.Add(new DataColumn("TotalWeekCountBoth",                         System.Type.GetType("System.Int32")));


            // Write array data to DataTable.  Loop over Prod-Prod combinations
            for (int i = 1; i < NUM_PRODS; i++)
            {
                // I think this could probably start with e2 = e1 given we only consider
                // e2 > e1 in ProcessCustomer and ProcessBasket
                for (int j = 1; j < NUM_PRODS; j++)
                {
                    if (ProdProdMetric[i, j, (int)ProdProdMetrics.CustomersBuyingBoth] > 0)
                    {
                        DataRow row = ProdProd.NewRow();
                        row["ModelID"] = MODEL_ID;
                        row["ModelDesc"] = MODEL_DESC;
                        row["Prod1"] = i;
                        row["Prod2"] = j;
                        row["CustomersBuyingBoth"] = ProdProdMetric[i, j, (int)ProdProdMetrics.CustomersBuyingBoth];
                        row["AnyBasketOfCustomersBuyingBoth"] = ProdProdMetric[i, j, (int)ProdProdMetrics.AnyBasketOfCustomersBuyingBoth];
                        row["BasketsContainingA_FromCustomersBuyingBoth"] = ProdProdMetric[i, j, (int)ProdProdMetrics.BasketsContainingA_FromCustomersBuyingBoth];
                        row["BasketsContainingB_FromCustomersBuyingBoth"] = ProdProdMetric[i, j, (int)ProdProdMetrics.BasketsContainingB_FromCustomersBuyingBoth];
                        row["BasketsContainingBoth"] = ProdProdMetric[i, j, (int)ProdProdMetrics.BasketsContainingBoth];
                        row["TotalStoreCountBoth"] = ProdProdMetric[i, j, (int)ProdProdMetrics.TotalStoreCountBoth];
                        row["TotalWeekCountBoth"] = ProdProdMetric[i, j, (int)ProdProdMetrics.TotalWeekCountBoth];
                        ProdProd.Rows.Add(row);
                    }
                }
            }

            return ProdProd;
        }

        private static DataTable MakeProdTable()
        {
            DataTable Prod = new DataTable("Products");

            Prod.Columns.Add(new DataColumn("ModelID", System.Type.GetType("System.Int32")));
            Prod.Columns.Add(new DataColumn("ModelDesc", System.Type.GetType("System.String")));
            Prod.Columns.Add(new DataColumn("Prod"       , System.Type.GetType("System.Int32")));
            Prod.Columns.Add(new DataColumn("CustomerCount", System.Type.GetType("System.Int32")));
            Prod.Columns.Add(new DataColumn("BasketCount", System.Type.GetType("System.Int32")));
            Prod.Columns.Add(new DataColumn("WeekCount"  , System.Type.GetType("System.Int32")));
            Prod.Columns.Add(new DataColumn("StoreCount" , System.Type.GetType("System.Int32")));


            // Write array data to DataTable.  Loop over Prod combinations
            for (int i = 1; i < NUM_PRODS; i++)
            {
                DataRow row = Prod.NewRow();
                row["ModelID"] = MODEL_ID;
                row["ModelDesc"] = MODEL_DESC;
                row["Prod"] = i;
                row["CustomerCount"] = ProdMetric[i, (int)ProdMetrics.CustomerCount];
                row["BasketCount"]   = ProdMetric[i, (int)ProdMetrics.BasketCount];
                row["WeekCount"  ]   = ProdMetric[i, (int)ProdMetrics.WeekCount  ];
                row["StoreCount" ]   = ProdMetric[i, (int)ProdMetrics.StoreCount ];
                Prod.Rows.Add(row);
            }

            return Prod;
        }

        private static DataTable MakeTotalsTable()
        {
            DataTable Total = new DataTable("Totals");

            Total.Columns.Add(new DataColumn("ModelID", System.Type.GetType("System.Int32")));
            Total.Columns.Add(new DataColumn("ModelDesc", System.Type.GetType("System.String")));
            Total.Columns.Add(new DataColumn("TotalCustomers", System.Type.GetType("System.Int32")));
            Total.Columns.Add(new DataColumn("TotalBaskets", System.Type.GetType("System.Int32")));
            Total.Columns.Add(new DataColumn("TotalWeeks"  , System.Type.GetType("System.Int32")));
            Total.Columns.Add(new DataColumn("TotalStores" , System.Type.GetType("System.Int32")));

            // Write array data to DataTable.
            DataRow row = Total.NewRow();
            row["ModelID"] = MODEL_ID;
            row["ModelDesc"] = MODEL_DESC;
            row["TotalCustomers"] = TotalCustomers;
            row["TotalBaskets"] = TotalBaskets;
            row["TotalWeeks"] = TotalWeeks;
            row["TotalStores"] = TotalStores;
            Total.Rows.Add(row);

            return Total;
        }


        private static void WriteOut()
        {
            
            // You'll have to write this method depending on where you want to write output

            // Later code assumes it is to a location you can later execute SQL against

            // You need to output 3 DataTable objects:
            //    MakeProdProdTable() --> SimRole.ProdProd
            //    MakeProdTable()     --> SimRole.Prod
            //    MakeTotalsTable()   --> SimRole.Totals

            // To build final SimRole output on these 2 tables, run the SimRoleFinalStep.sql file.

        }
        
        private static void ProcessCustomer(HashSet<int> CustomerProdSet)
        {

            // This method is called once a complete customer has been read from the input data file

            // Find all combinations of products that the customer has puchased ...
            foreach (int e1 in CustomerProdSet)
            {
                foreach (int e2 in CustomerProdSet)
                {
                    // Only process half the Product-Product combinations
                    // This saves half the processing time
                    // (If only there was a way to save half the memory footprint of the ProdProdMetric array)
                    if (e2 > e1)
                    {
                        // Variable & enum names hopefully explain what each statement is doing
                        ProdProdMetric[e1, e2, (int)ProdProdMetrics.CustomersBuyingBoth] += 1;
                        ProdProdMetric[e1, e2, (int)ProdProdMetrics.AnyBasketOfCustomersBuyingBoth] += NumBasketsForCustomer;
                        ProdProdMetric[e1, e2, (int)ProdProdMetrics.BasketsContainingA_FromCustomersBuyingBoth] += ProdCount_ThisCustomer[e1];
                        ProdProdMetric[e1, e2, (int)ProdProdMetrics.BasketsContainingB_FromCustomersBuyingBoth] += ProdCount_ThisCustomer[e2];
                    }
                }
                ProdMetric[e1, (int)ProdMetrics.CustomerCount]++;
            }

            // Reset ready for next customer
            NumBasketsForCustomer = 0;

            for (int i=0; i<NUM_PRODS; i++)
            {
                ProdCount_ThisCustomer[i] = 0;
            }

            // Protect against weird customers buying no products
            if (CustomerProdSet.Count > 0)
            {
                // Incremental other customer counts
                TotalCustomers++;
            }

        }
            
        private static void ProcessBasket(HashSet<int> BasketProdSet)
        {
            
            // This method is called once a complete basket has been read from the input data file

            // For each Product within the basket ...
            foreach (int e1 in BasketProdSet)
            {
                // Incremental the basket count for this Product
                ProdMetric[e1, (int)ProdMetrics.BasketCount] += 1;

                // Increment the basket count for this Product belong to just the current customer
                EntCount_ThisCustomer[e1] += 1;

                // Find all combinations of  products within the basket
                foreach (int e2 in BasketProdSet)
                {
                    // Only process half the Product-Product combinations
                    // This saves half the processing time
                    // (If only there was a way to save half the memory footprint of the ProdProdMetric array)
                    if (e2 > e1)
                    {
                        // Incremental the basket count for this Product-Product
                        ProdProdMetric[e1, e2, (int)ProdProdMetrics.BasketsContainingBoth] += 1;
                    }
                }
            }

            // Protect against weird baskets with no  products
            if (BasketProdSet.Count > 0)
            {
                // Incremental other basket counts
                NumBasketsForCustomer++;
                TotalBaskets++;
            }
        }

    }
}