using System;
using System.Linq;

//Write a program that takes an array of prices, and for each price greater than 20, it calculates a discount of 10% , greater then or  //equal to   50 then 25 %, else no discount

namespace HelloWorld
{
	class Prices
    {
    	double[] price = {12.30, 33.89, 56.10,9, 67.50,44,32.99,19};
        
      /* public void ApplyDiscount()
       {
            
            //Declare discountedPrice here so it's always available to avoid variable discountedPrice is being referenced
            //outside the scope where it was declared.
			//In C#, if you declare a variable inside an if or foreach block, it only exists inside that block.
            //If you try to use it elsewhere, you’ll get CS0103.
            double discountedPrice;
            
            foreach (double i in price)
            {
            	//Applying the 10% discount
                
                if(i > 20 && i < 50)
                {	
                	//i * 0.90 means multiplying the price by 90% (10% off).
                	discountedPrice = Math.Round(i * 0.90, 2); 
                    //Math.Round(..., 2) rounds the result to 2 decimal places for currency formatting.
                 }
                 else if(i >= 50)
                 {
                 	discountedPrice = Math.Round(i * 0.75, 2);
                    
                  }
                 else
                 {
                 	discountedPrice = i; //No discount
                 }
                 
                
                //Console.WriteLine($"{i:F2}\t\t{discountedPrice:F2}"); //Prints in table form
                //results.Add((i, discountedPrice));   
             
                
            }
            */
           
           //The LINQ query calculates all discounts same as the above method, but it also sorts the prices,
           //Transforms each value into an object:
           public void ApplyDiscount()
			{
            	// Create a variable called sortedPrice.
   				// 'var' tells C# to automatically determine the type.
    			// The result will be a collection of objects containing
    			// OriginalPrice and DiscountedPrice properties.
                
                
    			var sortedPrice = price
                // Select() transforms each item from the price array.
        		// 'p' represents the current price being processed.
       			 .Select(p => new
        			{
                    	// Store the original price for later display.
            			OriginalPrice = p,
                        
                        // Calculate the discounted price using a nested
            			// conditional (ternary) operator.
            			DiscountedPrice = p > 20 && p < 50
                        
                        // If price is greater than 20 and less than 50,
                		// apply a 10% discount.
                		// Example: 40 becomes 36.
               			? Math.Round(p * 0.90, 2)
                        
                        // If the first condition is false,
                		// check whether the price is 50 or more.
                			: p >= 50
                            
                            // Apply a 25% discount.
                    		// Example: 80 becomes 60.
                    			? Math.Round(p * 0.75, 2)
                                
                                // If neither condition is true,
                    			// no discount is applied.
                    			: p
        			})
                    
                    // OrderBy() sorts the resulting objects.
        			// priceInfo represents each object created in Select().
        			// Sorting is performed using the DiscountedPrice value.
       				 // Smallest DiscountedPrice appears first.
        			.OrderBy(priceInfo => priceInfo.DiscountedPrice);
				
                // Loop through the sorted collection.
    			foreach (var item in sortedPrice)
    			{
                	// Display the OriginalPrice and DiscountedPrice.
        			// F2 formats numbers to exactly 2 decimal places.
        			// \t inserts a tab space between columns.
        			Console.WriteLine($"R{item.OriginalPrice:F2}\tR{item.DiscountedPrice:F2}");
    			}
			}
           
            
   
   	 	static void Main(string[] args)
     	{
        	Console.WriteLine($"Original Price:\tDiscounted Price (10% / 25%):");
            
            
            //Exception Handling
   			try
        	{
        		Prices obj = new Prices();
            	obj.ApplyDiscount();   
            
         	}
         	catch(Exception e)
         	{
         		Console.WriteLine("An error occurred: " + e.Message);
        
      		}
      	}
    }
        
 }








//Modify the code to accept user input , instead of an already designed array.===== use linked lists afterward and create  a test class for multiple scenarios.
// Create a UI whereby the user enters the input data either into the array or linked list, then sort the discounted prices in ascending order.
  	

