(function ($) {
    "use strict"

    //date picker classic default
    $('.datepicker-default').pickadate({
        // Strings and translations
        monthsFull: ["ژانویه","فوریه","مارس","آوریل","مه","ژوئن","ژوئیه","اوت","سپتامبر","اکتبر","نوامبر","دسامبر"],
        monthsShort: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'],
        weekdaysFull: ['یک‌شنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه', 'شنبه'],
        weekdaysShort: ['ی', 'د', 'س', 'چ', 'پ', 'ج', 'ش'],
        showMonthsShort: undefined,
        showWeekdaysFull: undefined,

        // Buttons
        today: 'امروز',
        clear: 'پاک کردن',
        close: 'بستن',
    });

})(jQuery);