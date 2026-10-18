let dataSet = [
    [ "Tiger Nixon", "System Architect", "Edinburgh", "5421", "2011/04/25", "$320,800" ],
    [ "Garrett Winters", "Accountant", "Tokyo", "8422", "2011/07/25", "$170,750" ],
    [ "اشتون کاس", "تکنیکال Author", "San Francisco", "1562", "1395/01/12", "$86,000" ],
    [ "سدریک کولی", "Senior Javascript دولوپر", "Edinburgh", "6224", "2012/03/29", "$433,060" ],
    [ "اری ساتو", "Accountant", "Tokyo", "5407", "1398/11/28", "$162,700" ],
    [ "بریل ویلسون", "طراح متخصص", "New York", "4804", "2012/12/02", "$372,000" ],
    [ "هرولد چندلر", "دستیار فروش", "San Francisco", "9608", "2012/08/06", "$137,500" ],
    [ "رونا دیدسون", "طراح متخصص", "Tokyo", "6200", "1396/10/14", "$327,900" ],
    [ "کالین هاست", "Javascript دولوپر", "San Francisco", "2360", "1395/09/15", "$205,500" ],
    [ "سونیا فروست", "مهندس نرم افزار", "Edinburgh", "1667", "1398/12/13", "$103,600" ],
    [ "جنا جین", "مدیر دفتر", "London", "3814", "1398/12/19", "$90,560" ],
    [ "کوین فلن", "دستیار پشتیبانی", "Edinburgh", "9497", "1396/03/03", "$342,000" ],
    [ "چرالد مارشال", "کارگردان", "San Francisco", "6741", "1398/10/16", "$470,600" ],
    [ "هالی کندی", "سنیور مارکتینگ Designer", "London", "3597", "2012/12/18", "$313,500" ],
    [ "تانیا فیریزتریک", "کارگردان", "London", "1965", "2010/03/17", "$385,750" ],
    [ "میشل سیلوا", "Marketing Designer", "London", "1581", "2012/11/27", "$198,500" ],
    [ "پل برد", "Chief مدیر مالی (CFO)", "New York", "3059", "2010/06/09", "$725,000" ],
    [ "گلوریا لیتل", "مدیر سیستم", "New York", "1721", "1395/04/10", "$237,500" ],
    [ "بردلی گریر", "مهندس نرم افزار", "London", "2558", "2012/10/13", "$132,000" ],
    [ "دای روس", "مشاور", "Edinburgh", "2290", "2012/09/26", "$217,500" ],
    [ "جنت کادوی", "مسئول توسعه", "New York", "1937", "2011/09/03", "$345,000" ],
    [ "یوری بری", "Chief Marketing Officer (CMO)", "New York", "6154", "1395/06/25", "$675,000" ],
    [ "سیروس وانس", "پیش فروش", "New York", "8330", "1395/12/12", "$106,450" ],
    [ "دوریس ویلی", "دستیار فروش", "Sidney", "3023", "2010/09/20", "$85,600" ],
    [ "انجیل روماس", "Chief مدیر اجرایی (CEO)", "London", "5797", "1391/10/09", "$1,200,000" ],
    [ "گوین جوی", "دولوپر", "Edinburgh", "8822", "1396/12/22", "$92,575" ],
    [ "Jennifer Chang", "کارگردان", "Singapore", "9239", "1396/11/14", "$357,650" ],
    [ "برندن وینگر", "مهندس نرم افزار", "San Francisco", "1314", "2011/06/07", "$206,850" ],
    [ "فیونا گرین", "Chief اپراتور (COO)", "San Francisco", "2947", "2010/03/11", "$850,000" ],
    [ "سائو ایت", "بازاریاب", "Tokyo", "8899", "2011/08/14", "$163,000" ],
    [ "Michelle House", "طراح متخصص", "Sidney", "2769", "2011/06/02", "$95,400" ],
    [ "Suki Burks", "دولوپر", "London", "6832", "1391/10/22", "$114,500" ],
    [ "Prescott Bartlett", "Technical Author", "London", "3606", "2011/05/07", "$145,000" ],
    [ "Gavin Cortez", "Team Leader", "San Francisco", "2860", "1398/10/26", "$235,500" ],
    [ "Martena Mccray", "Post-Sales support", "Edinburgh", "8240", "2011/03/09", "$324,050" ],
    [ "Unity Butler", "Marketing Designer", "San Francisco", "5384", "1391/12/09", "$85,675" ]
];




(function($) {
    "use strict"
    //example 1
    var table = $('#example').DataTable({
        createdRow: function ( row, data, index ) {
           $(row).addClass('selected')
        } 
    });
      
    table.on('click', 'tbody tr', function() {
    var $row = table.row(this).nodes().to$();
    var hasClass = $row.hasClass('selected');
    if (hasClass) {
        $row.removeClass('selected')
    } else {
        $row.addClass('selected')
    }
    })
    
    table.rows().every(function() {
    this.nodes().to$().removeClass('selected')
    });



    //example 2
    var table2 = $('#example2').DataTable( {
        createdRow: function ( row, data, index ) {
            $(row).addClass('selected')
        },

        "scrollY":        "42vh",
        "scrollCollapse": true,
        "paging":         false
    });

    table2.on('click', 'tbody tr', function() {
        var $row = table2.row(this).nodes().to$();
        var hasClass = $row.hasClass('selected');
        if (hasClass) {
            $row.removeClass('selected')
        } else {
            $row.addClass('selected')
        }
    })
        
    table2.rows().every(function() {
        this.nodes().to$().removeClass('selected')
    });
	
	// 
	var table = $('#example3, #example4, #example5').DataTable();
	$('#example tbody').on('click', 'tr', function () {
		var data = table.row( this ).data();
	});
   
})(jQuery);