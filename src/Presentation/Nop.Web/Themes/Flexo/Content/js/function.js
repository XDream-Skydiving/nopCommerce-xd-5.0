/*
 * Flexo by NopAdvance team
 * Copyright (c) 2026 NopAdvance LLP. All rights reserved.
 * Proprietary software. Unauthorized redistribution is prohibited.
 * License terms: https://nopadvance.com/eula
 */

$(window).scroll(function(){
    if ($(window).scrollTop() >= 120) {
        $('.master-wrapper-page').addClass('headerfixed');
        $('.small-search-box-form', '.store-search-box').css("display", "none");
    }
    else {
        $('.master-wrapper-page').removeClass('headerfixed');
        $('.small-search-box-form', '.store-search-box').css("display", "block");
    }
});

/****************Mobile search******************/
$(document).ready(function () {
    $(".searchclick").click(function () {
        $(".small-search-box-form").slideToggle('fast');
    });

	$(".mobilemenulink").click(function(){
	    $("body").toggleClass("showmenu");
	});
	$(".closemenu").click(function(){
	    $("body").removeClass("showmenu");
	});	

	$(".myaccount").click(function(){
	    $("ul.myaccount-dropdown").slideToggle("show");
	});		


    $(".mobile-search-logo").click(function () {
        $(".mobilesearch").slideToggle("slow");
    });	

});