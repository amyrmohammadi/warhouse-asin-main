document.querySelector(".sweet-wrong").onclick = function () {
  sweetAlert("نه...", "یه اشتباهی رخ داده !!", "error")
}, document.querySelector(".sweet-message").onclick = function () {
  swal("سلام، یک پیام اینجاست !!")
}, document.querySelector(".sweet-text").onclick = function () {
  swal("سلام، یک پیام اینجاست !!", "جالبه نه؟")
}, document.querySelector(".sweet-success").onclick = function () {
  swal("هی چه خوب !!", " تو روی دکمه کلیک کردی!!", "success")
}, document.querySelector(".sweet-confirm").onclick = function () {
  swal({
    title: "مطمئنی میخوای حذف کنی ?",
    text: "دیکه به این فایل دسترسی نخواهی داشت !!",
    type: "warning",
    showCancelButton: !0,
    confirmButtonColor: "#DD6B55",
    confirmButtonText: "آره پاکش کن!!",
    closeOnConfirm: !1
  }, function () {
    swal("پاک شد", "فایل موردنظرت پاک شد", "success")
  })
}, document.querySelector(".sweet-success-cancel").onclick = function () {
  swal({
    title: "مطمئنی میخوای حذف کنی ?",
    text: "دیکه به این فایل دسترسی نخواهی داشت !!",
    type: "warning",
    showCancelButton: !0,
    confirmButtonColor: "#DD6B55",
    confirmButtonText: "آره پاکش کن!!",
    cancelButtonText: "نه لغو کن!!",
    closeOnConfirm: !1,
    closeOnCancel: !1
  }, function (e) {
    e ? swal("پاک شد", "فایل موردنظرت پاک شد", "success") : swal("کنسل شد!!", "فایلت باقی مونده", "error")
  })
}, document.querySelector(".sweet-image-message").onclick = function () {
  swal({
    title: "سوییت!!",
    text: "سلام یه تصویر اینجاست !!",
    imageUrl: "../assets/images/hand.jpg"
  })
}, document.querySelector(".sweet-html").onclick = function () {
  swal({
    title: "سوییت!!",
    text: "<span style='color:#ff0000'>سلام اینجا اچ تی ام ال داره!!<span>",
    html: !0
  })
}, document.querySelector(".sweet-auto").onclick = function () {
  swal({
    title: "پیام خودکار بسته میشه !!",
    text: "سلام من تو دو ثانیه بسته میشم!!",
    timer: 2e3,
    showConfirmButton: !1
  })
}, document.querySelector(".sweet-prompt").onclick = function () {
  swal({
    title: "ورودی رو وارد کن !!",
    text: "یه چیزی بنویس !!",
    type: "input",
    showCancelButton: !0,
    closeOnConfirm: !1,
    animation: "slide-from-top",
    inputPlaceholder: "Write something"
  }, function (e) {
    return !1 !== e && ("" === e ? (swal.showInputError("باید یه چیزی بنویسی!!!"), !1) : void swal("هی !!", "تو نوشتی: " + e, "success"))
  })
}, document.querySelector(".sweet-ajax").onclick = function () {
  swal({
    title: "سوییت اجاکس!!",
    text: "برای ران شدن تایید کن!!",
    type: "info",
    showCancelButton: !0,
    closeOnConfirm: !1,
    showLoaderOnConfirm: !0
  }, function () {
    setTimeout(function () {
      swal("درخواستت انجام شد !!")
    }, 2e3)
  })
};